using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>Outcome of running an external secret CLI (`op`, `bw`). <see cref="NotFound" /> is Node's `ENOENT`.</summary>
public record SecretCliResult(int ExitCode, string Stdout, bool NotFound = false);

/// <remarks>
/// Shared process seam for resolveOnePassword.js / resolveBitwardenPassword.js (`execFile` / `execFileSync` with
/// `encoding: 'utf8', windowsHide: true`, stdin closed). Swappable so unit tests never spawn a real `op`/`bw`.
/// </remarks>
public static class SecretCli
{
    private static readonly Func<string, string[], IReadOnlyDictionary<string, string>?, SecretCliResult> DefaultExec =
        (command, args, env) => ExecProcess(command, args, env);

    private static readonly Func<bool> DefaultIsInteractive = () =>
        !Console.IsInputRedirected && !Console.IsErrorRedirected;

    public static Func<string, string[], IReadOnlyDictionary<string, string>?, SecretCliResult> Exec
    {
        get;
        internal set;
    } = DefaultExec;

    /// <summary>resolveBitwardenPassword.js: `process.stdin.isTTY && process.stderr.isTTY`.</summary>
    public static Func<bool> IsInteractive { get; internal set; } = DefaultIsInteractive;

    /// <summary>Like <see cref="Exec" /> with text written to the child's stdin (native stores: `secret-tool store`).</summary>
    public static Func<string, string[], string?, SecretCliResult> ExecWithInput { get; internal set; } =
        (command, args, input) => ExecProcess(command, args, null, input);

    internal static void ResetToDefaults()
    {
        Exec = DefaultExec;
        ExecWithInput = (command, args, input) => ExecProcess(command, args, null, input);
        IsInteractive = DefaultIsInteractive;
    }

    /// <summary>
    /// Node's `execFile('name')` lookup (libuv): the current directory first, then each PATH entry in order; on Windows
    /// only `.com` and `.exe` files count (never `.cmd`/`.bat`/`.ps1`), and the System32 directory is not searched
    /// before PATH the way `CreateProcess` would. Null is `ENOENT`.
    /// </summary>
    internal static string? ResolveExecutable(string command)
    {
        bool hasDirectory = command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar);
        IEnumerable<string> directories = hasDirectory
            ? [""]
            : [Environment.CurrentDirectory, .. (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)];

        foreach (string directory in directories)
        {
            foreach (string name in CandidateNames(command))
            {
                string candidate = directory == "" ? name : Path.Combine(directory.Trim('"'), name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateNames(string command)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [command];
        }

        string extension = Path.GetExtension(command).ToLowerInvariant();

        return extension is ".exe" or ".com" ? [command] : [command + ".com", command + ".exe"];
    }

    private static SecretCliResult ExecProcess(
        string command, string[] args, IReadOnlyDictionary<string, string>? env, string? input = null)
    {
        ProcessStartInfo psi = new()
        {
            StandardInputEncoding = new UTF8Encoding(false),
            FileName = command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            CreateNoWindow = true
        };

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        if (env != null)
        {
            foreach ((string key, string value) in env)
            {
                psi.EnvironmentVariables[key] = value;
            }
        }

        string? resolved = ResolveExecutable(command);
        if (resolved is null)
        {
            return new SecretCliResult(-1, "", true);
        }

        psi.FileName = resolved;

        Process process;
        try
        {
            process = Process.Start(psi)!;
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 2)
        {
            return new SecretCliResult(-1, "", true);
        }
        catch (Win32Exception)
        {
            return new SecretCliResult(-1, "");
        }

        using (process)
        {
            if (input != null)
            {
                process.StandardInput.Write(input);
            }

            process.StandardInput.Close();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stderr.Wait();

            return new SecretCliResult(process.ExitCode, stdout);
        }
    }
}
