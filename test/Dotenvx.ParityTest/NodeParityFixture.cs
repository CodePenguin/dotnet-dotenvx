using System.Diagnostics;
using System.Text;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Runs the real Node dotenvx CLI (from the dotenvx/ submodule) and our built .NET CLI as real
/// subprocesses so parity tests compare the actual shipped artifacts, not in-process internals.
/// Shared once per test run via [CollectionDefinition] below. If Node/npm aren't available, or the
/// submodule isn't checked out, tests skip themselves via IsAvailable rather than failing the build -
/// this suite is opt-in/manual-first, not a hard requirement to develop in this repo.
/// </remarks>
public sealed class NodeParityFixture : IAsyncLifetime
{
    public string RepoRoot { get; }
    public string DotenvxJsPath { get; }
    public string DotenvxDllPath { get; }
    public bool IsAvailable { get; private set; }
    public string UnavailableReason { get; private set; } = "";

    private string _nodePath = "node";
    private string _dotnetPath = "dotnet";

    public NodeParityFixture()
    {
        RepoRoot = FindRepoRoot();
        DotenvxJsPath = Path.Combine(RepoRoot, "dotenvx", "src", "cli", "dotenvx.js");
        DotenvxDllPath = Path.Combine(AppContext.BaseDirectory, "dotenvx.dll");
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(DotenvxJsPath))
        {
            UnavailableReason =
                $"dotenvx submodule not checked out ({DotenvxJsPath} not found) - run: git submodule update --init --recursive";
            return;
        }

        if (!File.Exists(DotenvxDllPath))
        {
            UnavailableReason = $"built CLI not found at {DotenvxDllPath} - build src/Dotenvx first";
            return;
        }

        string? nodePath = FindOnPath("node");
        if (nodePath == null)
        {
            UnavailableReason = "node not found on PATH";
            return;
        }

        _nodePath = nodePath;

        string? npmPath = FindOnPath("npm");
        if (npmPath == null)
        {
            UnavailableReason = "npm not found on PATH";
            return;
        }

        // the host that is running the tests: a `dotnet` found on PATH can be a broken link (seen on a macOS machine)
        string? dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host) ? host : FindOnPath("dotnet");
        if (dotnetPath != null)
        {
            _dotnetPath = dotnetPath;
        }

        string dotenvxDir = Path.Combine(RepoRoot, "dotenvx");
        if (!Directory.Exists(Path.Combine(dotenvxDir, "node_modules")))
        {
            Console.WriteLine("[NodeParityFixture] Installing dotenvx submodule npm dependencies...");
            CliResult install = await RunProcessAsync(npmPath, ["install", "--no-audit", "--no-fund", "--omit=dev"],
                dotenvxDir);
            if (install.ExitCode != 0)
            {
                UnavailableReason = $"npm install failed (exit {install.ExitCode}): {install.StandardError}";
                return;
            }
        }

        IsAvailable = true;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public Task<CliResult> RunNodeAsync(string workingDirectory, IReadOnlyDictionary<string, string>? env,
        params string[] args)
    {
        return RunProcessAsync(_nodePath, [DotenvxJsPath, ..args], workingDirectory, env);
    }

    /// <summary>Runs the Node CLI with <paramref name="input" /> piped to stdin ("" = closed stdin).</summary>
    public Task<CliResult> RunNodeWithInputAsync(string workingDirectory, IReadOnlyDictionary<string, string>? env,
        string input, params string[] args)
    {
        return RunProcessAsync(_nodePath, [DotenvxJsPath, ..args], workingDirectory, env, input);
    }

    /// <summary>Runs a Node script file from the `dotenvx/` directory with <paramref name="input" /> piped to stdin.</summary>
    public Task<CliResult> RunNodeFileWithInputAsync(string file, string input, params string[] args)
    {
        return RunProcessAsync(_nodePath, [file, ..args], Path.Combine(RepoRoot, "dotenvx"), null, input);
    }

    /// <summary>Runs a Node oracle script file with <paramref name="workingDirectory" /> as its current directory.</summary>
    public Task<CliResult> RunNodeOracleAsync(string workingDirectory, IReadOnlyDictionary<string, string>? env, params string[] args)
    {
        return RunProcessAsync(_nodePath, args, workingDirectory, env);
    }

    public string NodePath => _nodePath;

    public string DotnetPath => _dotnetPath;

    /// <summary>
    /// Runs test/tools/pty/run.cjs (a pseudo terminal plus a terminal emulator that reads the screen) on a spec file. Installs its
    /// npm dependencies on first use; returns null when they cannot be installed.
    /// </summary>
    public async Task<CliResult?> RunPtyAsync(string specFile)
    {
        string tools = Path.Combine(RepoRoot, "test", "tools", "pty");
        if (!Directory.Exists(Path.Combine(tools, "node_modules", "node-pty")))
        {
            string npm = FindOnPath("npm") ?? "npm";
            CliResult install = await RunProcessAsync(npm, ["install", "--no-audit", "--no-fund"], tools);
            if (install.ExitCode != 0)
            {
                return null;
            }
        }

        // npm leaves node-pty's prebuilt spawn-helper without its execute bit on macOS ("posix_spawnp failed")
        if (!OperatingSystem.IsWindows())
        {
            string prebuilds = Path.Combine(tools, "node_modules", "node-pty", "prebuilds");
            if (Directory.Exists(prebuilds))
            {
                foreach (string helper in Directory.GetFiles(prebuilds, "spawn-helper", SearchOption.AllDirectories))
                {
                    File.SetUnixFileMode(helper, File.GetUnixFileMode(helper) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }
        }

        return await RunProcessAsync(_nodePath, [Path.Combine(tools, "run.cjs"), specFile], tools);
    }

    public Task<CliResult> RunDotnetWithInputAsync(string workingDirectory, IReadOnlyDictionary<string, string>? env,
        string input, params string[] args)
    {
        return RunProcessAsync(_dotnetPath, [DotenvxDllPath, ..args], workingDirectory, env, input);
    }

    public Task<CliResult> RunDotnetAsync(string workingDirectory, IReadOnlyDictionary<string, string>? env,
        params string[] args)
    {
        return RunProcessAsync(_dotnetPath, [DotenvxDllPath, ..args], workingDirectory, env);
    }

    /// <summary>
    /// Runs an inline Node script (the "live oracle" technique - see docs/porting-traps.md, `@dotenvx/
    /// primitives` section) from within the `dotenvx/` submodule directory, so `require('./src/
    /// lib/...')`-relative paths resolve the same way they would from a real file in that tree.
    /// </summary>
    public async Task<CliResult> RunNodeScriptAsync(string script, IReadOnlyDictionary<string, string>? env = null)
    {
        if (script.Length < 20000)
        {
            return await RunProcessAsync(_nodePath, ["-e", script], Path.Combine(RepoRoot, "dotenvx"), env);
        }

        // long scripts overflow the Windows command-line limit - run them from a temp file instead
        string file = Path.Combine(Path.GetTempPath(), $"oracle-{Guid.NewGuid():N}.cjs");
        await File.WriteAllTextAsync(file, script);
        try
        {
            return await RunProcessAsync(_nodePath, [file], Path.Combine(RepoRoot, "dotenvx"),
                new Dictionary<string, string> { ["NODE_PATH"] = Path.Combine(RepoRoot, "dotenvx", "node_modules") });
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static readonly string HermeticConfigDir =
        Path.Combine(Path.GetTempPath(), "dotenvx-parity-config-" + Guid.NewGuid().ToString("N"));

    private static async Task<CliResult> RunProcessAsync(string fileName, IReadOnlyList<string> args,
        string workingDirectory, IReadOnlyDictionary<string, string>? env = null, string? input = null)
    {
        ProcessStartInfo psi = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true, // never inherit the test host's stdin: a child that reads it would wait forever
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            CreateNoWindow = true
        };

        if (input != null)
        {
            psi.StandardInputEncoding = new UTF8Encoding(false);
        }

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        // hermetic by default: no developer Armor session or device key is ever read
        psi.EnvironmentVariables["DOTENVX_CONFIG"] = HermeticConfigDir;

        // native OS secret storage is the default since 2.34: keep the real store untouched unless a test opts in with
        // DOTENVX_NO_NATIVE=false (the fake-store tests)
        psi.EnvironmentVariables["DOTENVX_NO_NATIVE"] = "true";

        // scripts/parity-coverage.js: V8 coverage of the upstream source while the parity suite drives it
        if (Environment.GetEnvironmentVariable("DOTENVX_PARITY_NODE_COVERAGE") is { Length: > 0 } coverageDir &&
            Path.GetFileNameWithoutExtension(fileName).Equals("node", StringComparison.OrdinalIgnoreCase))
        {
            psi.EnvironmentVariables["NODE_V8_COVERAGE"] = coverageDir;
        }

        if (env != null)
        {
            foreach (KeyValuePair<string, string> kvp in env)
            {
                psi.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        using Process process = Process.Start(psi)!;
        if (input != null)
        {
            await process.StandardInput.WriteAsync(input);
        }

        process.StandardInput.Close();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        return new CliResult(process.ExitCode, stdout, stderr);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Dotenvx.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ??
               throw new InvalidOperationException(
                   "Could not locate repo root (no Dotenvx.sln found in any ancestor of AppContext.BaseDirectory)");
    }

    private static string? FindOnPath(string command)
    {
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        string[] extensions = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat"] : [""];
        foreach (string path in pathVar.Split(Path.PathSeparator))
        {
            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(path, command + ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}

public sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

[CollectionDefinition(Name)]
public sealed class NodeParityCollection : ICollectionFixture<NodeParityFixture>
{
    public const string Name = "NodeParity";
}
