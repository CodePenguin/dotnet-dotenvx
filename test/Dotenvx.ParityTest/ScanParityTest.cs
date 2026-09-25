using System.Diagnostics;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/ext/scan.js. Ext-only (no top-level `dotenvx scan`
/// exists upstream). Only the "gitleaks not on PATH" scenario is covered here - it's the one
/// scenario this suite can exercise deterministically without the real `gitleaks` binary
/// installed (this dev machine and ubuntu-latest CI both lack it by default), and it already
/// exercises the real subprocess-based CLI end to end (GitleaksHelper's actual Process.Start
/// failure path, not a mock). The "gitleaks IS installed and produces a real scan" scenario is
/// deliberately NOT added yet - gitleaks's own output isn't obviously byte-stable across
/// versions/environments (paths, timing), and writing that assertion without a real gitleaks
/// binary available to verify it against would mean shipping an unverified guess. Skips
/// gracefully (rather than failing) if gitleaks unexpectedly IS on PATH, since this specific
/// scenario needs it absent - the inverse of every other test's Node/npm-absence skip.
/// </remarks>
public class ScanParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static bool? _gitleaksAvailable;

    private static bool GitleaksAvailable()
    {
        if (_gitleaksAvailable.HasValue)
        {
            return _gitleaksAvailable.Value;
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "gitleaks",
                Arguments = "version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            process?.WaitForExit();
            _gitleaksAvailable = process is { ExitCode: 0 };
        }
        catch
        {
            _gitleaksAvailable = false;
        }

        return _gitleaksAvailable.Value;
    }

    [Fact]
    public async Task GitleaksNotOnPathProducesIdenticalErrorAndExitCode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        if (GitleaksAvailable())
        {
            Console.WriteLine("SKIPPED (gitleaks IS on PATH - this scenario needs it absent)");
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "ext", "scan");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardError.ShouldBe(node.StandardError);
    }
}
