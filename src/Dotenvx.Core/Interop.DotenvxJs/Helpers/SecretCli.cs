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
        ExecProcess;

    private static readonly Func<bool> DefaultIsInteractive = () =>
        !Console.IsInputRedirected && !Console.IsErrorRedirected;

    public static Func<string, string[], IReadOnlyDictionary<string, string>?, SecretCliResult> Exec
    {
        get;
        internal set;
    } = DefaultExec;

    /// <summary>resolveBitwardenPassword.js: `process.stdin.isTTY && process.stderr.isTTY`.</summary>
    public static Func<bool> IsInteractive { get; internal set; } = DefaultIsInteractive;

    internal static void ResetToDefaults()
    {
        Exec = DefaultExec;
        IsInteractive = DefaultIsInteractive;
    }

    private static SecretCliResult ExecProcess(
        string command, string[] args, IReadOnlyDictionary<string, string>? env)
    {
        ProcessStartInfo psi = new()
        {
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
            process.StandardInput.Close();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stderr.Wait();

            return new SecretCliResult(process.ExitCode, stdout);
        }
    }
}
