using System.Diagnostics;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No single upstream reference file - mirrors the two `child_process.execSync('git ...')` calls
/// inside precommit.js's `_isInGitRepo()`/`_isFileToBeCommitted()`. Delegate-swap statics (matching
/// Fsx.cs's convention) so Precommit's tests never spawn a real git process, and so the non-ASCII
/// filename regression test (see PrecommitTest.cs) is deterministic instead of depending on a real
/// repo/git-version/locale actually C-quoting a café/... path the way precommit.js's own code
/// comment describes.
/// </remarks>
public static class GitHelper
{
    public static Func<bool> IsInsideGitWorkTree { get; internal set; } = DefaultIsInsideGitWorkTree;

    // NUL-separated (git's `-z` flag), not line-separated - avoids git's C-quoting/octal-escaping
    // of non-ASCII paths, which would otherwise never string-match the plain filePath being checked.
    public static Func<string[]> DiffNameOnlyNulSeparated { get; internal set; } = DefaultDiffNameOnlyNulSeparated;

    internal static void ResetToDefaults()
    {
        IsInsideGitWorkTree = DefaultIsInsideGitWorkTree;
        DiffNameOnlyNulSeparated = DefaultDiffNameOnlyNulSeparated;
    }

    private static bool DefaultIsInsideGitWorkTree()
    {
        try
        {
            using Process process = StartGit("rev-parse --is-inside-work-tree");
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string[] DefaultDiffNameOnlyNulSeparated()
    {
        using Process process = StartGit("diff HEAD --name-only -z");
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("git diff failed");
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static Process StartGit(string arguments)
    {
        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        return process;
    }
}
