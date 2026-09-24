using System.Diagnostics;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No single upstream reference file - mirrors scan.js's two `child_process.execSync('gitleaks
/// ...')` calls. Deviates from this port's usual "inline in the action" placement for a single-
/// consumer helper (the pattern used for e.g. GitignoreAction's own logic) because the swap point
/// needs InternalsVisibleTo to be visible from Dotenvx.Test, and src/Dotenvx/Dotenvx.csproj (unlike
/// Dotenvx.Core.csproj) doesn't grant that - every other testable seam in this codebase lives in
/// Dotenvx.Core for the same reason, so this one does too, following the Fsx.cs/GitHelper.cs
/// delegate-swap convention exactly.
/// </remarks>
public static class GitleaksHelper
{
    // (ExitCode, CombinedOutput) - matches execSync capturing stdout+stderr merged via `2>&1`
    // for the detect call; the version-probe call only cares about the exit code.
    public static Func<string, (int ExitCode, string Output)> RunCommand { get; internal set; } = DefaultRunCommand;

    internal static void ResetToDefaults()
    {
        RunCommand = DefaultRunCommand;
    }

    private static (int, string) DefaultRunCommand(string arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gitleaks",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception)
        {
            return (1, "");
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout + stderr);
    }
}
