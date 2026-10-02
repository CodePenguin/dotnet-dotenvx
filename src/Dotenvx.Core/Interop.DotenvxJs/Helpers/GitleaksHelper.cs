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
///
/// Found via the static code-audit (see CLAUDE.md's "lower-priority findings" note): the original
/// implementation read stdout and stderr via two separate ReadToEnd() calls and concatenated them,
/// which loses chronological ordering if gitleaks ever writes to both streams in the same run -
/// unlike upstream's shell-level `2>&1`, which produces a genuinely interleaved combined stream
/// because the OS kernel serializes writes to a single merged pipe. Fixed by doing the SAME
/// shell-level merge here (`RunShellCommand`), rather than approximating it with .NET-side async
/// stream-reading - two independently-scheduled OutputDataReceived/ErrorDataReceived callbacks
/// reading from two separate pipes still can't guarantee cross-stream ordering, only the kernel-
/// level merge can. Verified against GitleaksHelperTest (Dotenvx.Core.Test), which points
/// RunShellCommand at a small FakeGitleaks fixture executable instead of the real `gitleaks`
/// binary, so this suite needs no gitleaks installation.
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

    private static (int, string) DefaultRunCommand(string arguments) => RunShellCommand($"gitleaks {arguments}");

    /// <summary>
    /// Runs <paramref name="commandLine"/> through the platform shell with its stderr redirected
    /// onto stdout (`2>&1`), returning the exit code and the single, chronologically-ordered
    /// combined stream - exactly what Node's `child_process.execSync('... 2>&1')` captures.
    /// Internal (not private) so GitleaksHelperTest can point it at a fixture executable instead of
    /// the real `gitleaks` binary named in <see cref="DefaultRunCommand"/>.
    ///
    /// Writes <paramref name="commandLine"/> to a temp script file and runs THAT, rather than
    /// passing it as a `cmd.exe /c "..."`/`sh -c "..."` command-line argument directly - a
    /// `commandLine` containing its own embedded quotes (e.g. a quoted path with spaces) gets a
    /// SECOND, conflicting layer of quoting/escaping applied when .NET builds the native argv for
    /// the shell process itself (confirmed live: cmd.exe in particular does not parse its own `/c`
    /// remainder using the same escaping rules .NET's `ProcessStartInfo` argument-encoding assumes,
    /// so a quoted inner path reliably breaks). A script file sidesteps this - the shell just reads
    /// ordinary script syntax from disk, with no re-quoting in between.
    /// </summary>
    internal static (int, string) RunShellCommand(string commandLine)
    {
        bool isWindows = OperatingSystem.IsWindows();
        string scriptPath = Path.Combine(Path.GetTempPath(), $"dotenvx-gitleaks-{Guid.NewGuid():N}{(isWindows ? ".cmd" : ".sh")}");
        File.WriteAllText(scriptPath, isWindows
            ? $"@echo off\r\n{commandLine} 2>&1\r\n"
            : $"#!/bin/sh\n{commandLine} 2>&1\n");

        try
        {
            ProcessStartInfo startInfo = isWindows
                ? new ProcessStartInfo { FileName = "cmd.exe", ArgumentList = { "/c", scriptPath } }
                : new ProcessStartInfo { FileName = "/bin/sh", ArgumentList = { scriptPath } };

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;

            using Process process = new() { StartInfo = startInfo };

            try
            {
                process.Start();
            }
            catch (Exception)
            {
                return (1, "");
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output);
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup - a stray temp file here isn't worth failing the call over.
            }
        }
    }
}
