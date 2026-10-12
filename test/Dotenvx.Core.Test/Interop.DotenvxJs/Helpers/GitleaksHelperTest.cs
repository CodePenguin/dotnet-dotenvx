using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Not from a JS test - no single upstream reference file exists for GitleaksHelper (see its own
/// doc comment). This locks in a real bug the 2026-09-29 static code audit found but left unfixed
/// (no `gitleaks` binary was available to verify a fix against): the original implementation read
/// stdout/stderr via two separate ReadToEnd() calls and concatenated them, discarding chronological
/// order between the two streams, unlike upstream's shell-level `2>&1` merge. Verifying a REAL fix
/// needs an actual child process whose stdout/stderr interleaving is under the test's control - a
/// fully mocked `GitleaksHelper.RunCommand` (as ScanActionTest uses) can't exercise this, since it
/// never spawns a process at all. Instead of requiring `gitleaks` itself, this points
/// `RunShellCommand` (the internal seam `DefaultRunCommand` delegates to) at FakeGitleaks, a tiny
/// fixture console app (test/FakeGitleaks) built and copied alongside this test assembly via
/// ProjectReference - see its own Program.cs for its `exitCode op1 op2...` argument shape.
/// </remarks>
public class GitleaksHelperTest
{
    private static readonly string FakeGitleaksDllPath =
        Path.Combine(AppContext.BaseDirectory, "FakeGitleaks.dll");

    [Fact]
    public void RunShellCommandPreservesChronologicalOrderAcrossStdoutAndStderr()
    {
        (int exitCode, string output) = GitleaksHelper.RunShellCommand(
            BuildFakeGitleaksCommand(0, "O:FIRST_OUT", "E:SECOND_ERR", "O:THIRD_OUT", "E:FOURTH_ERR"));

        exitCode.ShouldBe(0);

        int first = output.IndexOf("FIRST_OUT", StringComparison.Ordinal);
        int second = output.IndexOf("SECOND_ERR", StringComparison.Ordinal);
        int third = output.IndexOf("THIRD_OUT", StringComparison.Ordinal);
        int fourth = output.IndexOf("FOURTH_ERR", StringComparison.Ordinal);

        first.ShouldBeGreaterThanOrEqualTo(0);
        second.ShouldBeGreaterThanOrEqualTo(0);
        third.ShouldBeGreaterThanOrEqualTo(0);
        fourth.ShouldBeGreaterThanOrEqualTo(0);

        first.ShouldBeLessThan(second);
        second.ShouldBeLessThan(third);
        third.ShouldBeLessThan(fourth);
    }

    [Fact]
    public void RunShellCommandReturnsTheChildsRealExitCode()
    {
        (int exitCode, string output) = GitleaksHelper.RunShellCommand(
            BuildFakeGitleaksCommand(3, "E:leak:API_KEY=abcd1234"));

        exitCode.ShouldBe(3);
        output.ShouldContain("leak:API_KEY=abcd1234");
    }

    [Fact]
    public void RunShellCommandReturnsEmptyOutputWhenNoOpsAreGiven()
    {
        (int exitCode, string output) = GitleaksHelper.RunShellCommand(BuildFakeGitleaksCommand(0));

        exitCode.ShouldBe(0);
        output.Trim().ShouldBeEmpty();
    }

    private static string BuildFakeGitleaksCommand(int exitCode, params string[] ops) =>
        $"dotnet \"{FakeGitleaksDllPath}\" {exitCode} {string.Join(' ', ops)}";
}
