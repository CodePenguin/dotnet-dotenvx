using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <summary>
/// Shared harness for the Armor parity tests: a fake Armor API, a fixed device key and settings file in a per-run
/// DOTENVX_CONFIG directory, and a fake OS secret store (the `powershell`/`secret-tool` shim) on PATH so the access token
/// never touches the real one. <see cref="Compare" /> runs the Node CLI and the .NET CLI from identical state and asserts
/// that everything observable is identical: exit code, stdout, stderr, the request log of the fake API and the resulting
/// files (project, settings and secret store).
/// </summary>
public abstract class ArmorParityTestBase(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    protected const string DevicePrivateKey = "6a2b0bbd6d3c3c6dd5ebc8d2ae0c4e4f8c1b3e0e2b3b1e5d9f7e1d3e9b5a4c21";

    protected FakeArmorServer Server { get; } = new();

    protected string ConfigDir => Path.Combine(WorkDir, "cfg");
    protected string VaultDir => Path.Combine(WorkDir, "vault");
    protected string BinDir => Path.Combine(WorkDir, "fakebin");

    protected string DevicePublicKey => Primitives.Primitives.Derive(DevicePrivateKey);

    protected bool Available => EnsureAvailable() && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux());

    public override void Dispose()
    {
        Server.Dispose();
        base.Dispose();
    }

    /// <summary>What a single CLI run left behind.</summary>
    protected record Outcome(
        int ExitCode, string Stdout, string Stderr, string[] Requests, Dictionary<string, string> Files);

    /// <summary>Settings written into the config directory's `.env` before the run (null: no settings file at all).</summary>
    protected Dictionary<string, string>? Settings { get; set; }

    /// <summary>Redact key material in the project files (needed when the CLI creates fresh random keys).</summary>
    protected bool RedactFiles { get; set; }

    /// <summary>False for scenarios with prompts: enquirer's ANSI redraws are not reproduced byte for byte.</summary>
    protected bool CompareStderr { get; set; } = true;

    /// <summary>Redact key material in the logged request bodies (fresh random keys).</summary>
    protected bool RedactRequests { get; set; }

    /// <summary>The access token placed in the fake secret store before the run.</summary>
    protected string? StoredToken { get; set; }

    /// <summary>A logged-in session: hostname, user, username, on, token (in the secret store) and a recent update check.</summary>
    protected void LoggedIn(string username = "alice", bool on = true, string? token = "tok_abc1234567890")
    {
        Settings = new Dictionary<string, string>
        {
            ["DOTENVX_ARMOR_HOSTNAME"] = Server.BaseUrl,
            ["DOTENVX_ARMOR_USER"] = "usr_1",
            ["DOTENVX_ARMOR_USERNAME"] = username,
            ["DOTENVX_ARMOR_ON"] = on ? "true" : "false",
            // far in the future: no update check (and so no request to dotenvx.sh) ever fires
            ["DOTENVX_VERSION_LAST_CHECK"] = "99999999999999"
        };
        StoredToken = token;
    }

    protected Dictionary<string, string> Env(Dictionary<string, string>? extra = null)
    {
        var env = new Dictionary<string, string>
        {
            ["PATH"] = BinDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["FAKE_SECRET_CLI_DIR"] = VaultDir,
            ["DOTENVX_CONFIG"] = ConfigDir,
            ["CI"] = "",
            ["DOTENVX_NO_NATIVE"] = "false"
        };

        foreach ((string key, string value) in extra ?? [])
        {
            env[key] = value;
        }

        return env;
    }

    /// <summary>Wipes and recreates the fake world: config dir (settings + device key), secret store, project files.</summary>
    protected void Reset(Action? project = null)
    {
        foreach (string dir in new[] { ConfigDir, VaultDir })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(Path.Combine(VaultDir, "native"));
        FakeCliInstaller.Install(BinDir, OperatingSystem.IsWindows() ? "powershell" : "secret-tool");

        // the device key, in the same encrypted conf file both CLIs read
        var device = new ConfStore(ConfigDir, ".device-key", "dotenvxpro dotenvxpro dotenvxpro");
        device.Set("private_key/1", DevicePrivateKey);

        if (Settings is not null)
        {
            File.WriteAllText(Path.Combine(ConfigDir, ".env"),
                string.Join("\n", Settings.Select(entry => $"{entry.Key}=\"{entry.Value}\"")));
        }

        if (StoredToken is not null)
        {
            File.WriteAllText(Path.Combine(VaultDir, "native", "dotenvx_DOTENVX_ARMOR_TOKEN"),
                StoredToken);
        }

        foreach (string stale in new[] { ".env", ".env.keys" })
        {
            File.Delete(Path.Combine(WorkDir, stale));
        }

        project?.Invoke();
        Server.Clear();
    }

    private Dictionary<string, string> Snapshot()
    {
        var files = new Dictionary<string, string>();
        foreach (string name in new[] { ".env", ".env.keys", "example.out" })
        {
            string path = Path.Combine(WorkDir, name);
            if (File.Exists(path))
            {
                files[name] = RedactFiles ? RedactKeyMaterial(File.ReadAllText(path)) : File.ReadAllText(path);
            }
        }

        string settings = Path.Combine(ConfigDir, ".env");
        if (File.Exists(settings))
        {
            // `login` ends with the update check, which asks the real https://dotenvx.sh/VERSION and stamps the time: both
            // values are environment-dependent, so they are left out of the comparison
            files["config/.env"] = string.Join((char)10, File.ReadAllText(settings).Split((char)10)
                .Where(line => !line.StartsWith("DOTENVX_VERSION=") && !line.StartsWith("DOTENVX_VERSION_LAST_CHECK=")));
        }

        string native = Path.Combine(VaultDir, "native");
        if (Directory.Exists(native))
        {
            foreach (string file in Directory.GetFiles(native).Order())
            {
                string vaultName = Path.GetFileName(file);
                files["vault/" + (RedactFiles ? System.Text.RegularExpressions.Regex.Replace(vaultName, "[0-9a-f]{40,}", "REDACTED") : vaultName)] = File.ReadAllText(file);
            }
        }

        // the device key must never change: it is shared by both CLIs
        files["device"] = new Device(ConfigDir).PrivateKey();

        return files;
    }

    /// <summary>
    /// The CLIs hand their `POST /api/events` to a detached worker that outlives the command: wait until the request log has
    /// been quiet for a moment (or a few seconds passed) before reading it.
    /// </summary>
    private async Task SettleEvents()
    {
        int last = -1;
        DateTime quietSince = DateTime.UtcNow;
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            int count = Server.RequestLog().Length;
            if (count != last)
            {
                last = count;
                quietSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - quietSince > TimeSpan.FromMilliseconds(700))
            {
                return;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>Event timestamps and durations differ between any two runs.</summary>
    private static string NormalizeEvents(string line)
    {
        return line.Contains("/api/events")
            ? System.Text.RegularExpressions.Regex.Replace(
                System.Text.RegularExpressions.Regex.Replace(line, "\"occurred_at\":\"[^\"]*\"", "\"occurred_at\":\"T\""),
                "\"duration_ms\":[0-9]+", "\"duration_ms\":0")
            : line;
    }

    private async Task<Outcome> RunOne(bool node, Action? project, string? input, Dictionary<string, string> env,
        string[] args)
    {
        Reset(project);
        CliResult result = input is null
            ? node
                ? await Fixture.RunNodeAsync(WorkDir, env, args)
                : await Fixture.RunDotnetAsync(WorkDir, env, args)
            : node
                ? await Fixture.RunNodeWithInputAsync(WorkDir, env, input, args)
                : await Fixture.RunDotnetWithInputAsync(WorkDir, env, input, args);

        await SettleEvents();

        return new Outcome(result.ExitCode, result.StandardOutput, result.StandardError, [.. Server.RequestLog().Select(line => NormalizeEvents(RedactRequests ? RedactKeyMaterial(line) : line))],
            Snapshot());
    }

    /// <summary>Runs both CLIs from identical state and asserts identical outcomes; returns the Node outcome.</summary>
    protected async Task<Outcome> CompareWith(Action? project, string? input, Dictionary<string, string>? extraEnv,
        params string[] args)
    {
        Dictionary<string, string> env = Env(extraEnv);

        Outcome node = await RunOne(true, project, input, env, args);
        Outcome dotnet = await RunOne(false, project, input, env, args);

        dotnet.ExitCode.ShouldBe(node.ExitCode, $"stderr: {dotnet.Stderr}\nnode stderr: {node.Stderr}");
        dotnet.Stdout.ShouldBe(node.Stdout);
        if (CompareStderr)
        {
            dotnet.Stderr.ShouldBe(node.Stderr);
        }
        dotnet.Requests.ShouldBe(node.Requests);
        dotnet.Files.Keys.Order().ShouldBe(node.Files.Keys.Order());
        foreach ((string name, string content) in node.Files)
        {
            dotnet.Files[name].ShouldBe(content, $"file {name}");
        }

        return node;
    }

    protected Task<Outcome> Compare(params string[] args)
    {
        return CompareWith(null, null, null, args);
    }
}
