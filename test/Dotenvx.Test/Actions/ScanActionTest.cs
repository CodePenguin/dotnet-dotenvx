using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>
/// Reference source: dotenvx/tests/cli/actions/ext/scan.test.js. GitleaksHelper.RunCommand is
/// mocked in every test - no real `gitleaks` binary dependency for this fast, offline suite (an
/// end-to-end scenario belongs in Dotenvx.ParityTest instead, behind a graceful
/// gitleaks-not-on-PATH skip, matching the existing Node/npm-absence pattern there - not added in
/// this phase).
/// </remarks>
public class ScanActionTest : EnvironmentTestBase
{
    public ScanActionTest()
    {
        RootCommand = new LoggingRootCommand { new ScanAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "scan - gitleaks not installed"</remarks>
    [Fact]
    public void TestScanGitleaksNotInstalled()
    {
        GitleaksHelper.RunCommand = _ => (1, "");

        int exitCode = RootCommand.Parse(["scan"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("gitleaks: command not found");
        MockLogger.InfoMessages.ShouldContain("fix: install gitleaks:      [brew install gitleaks]");
        MockLogger.InfoMessages.ShouldContain("fix: other install options: [https://github.com/gitleaks/gitleaks]");
    }

    /// <remarks>Refer to "scan - gitleaks installed and works"</remarks>
    [Fact]
    public void TestScanGitleaksInstalledAndWorks()
    {
        const string gitleaksOutput = "10:22AM INF 0 commits scanned.\nscan completed in 14.4ms\nno leaks found\n";
        string[] calls = [];
        GitleaksHelper.RunCommand = arg =>
        {
            calls = [.. calls, arg];
            return arg == "version" ? (0, "8.18.4") : (0, gitleaksOutput);
        };

        int exitCode = RootCommand.Parse(["scan"]).Invoke();

        exitCode.ShouldBe(0);
        calls.ShouldBe(["version", "detect --no-banner --verbose"]);
        MockLogger.InfoMessages.ShouldContain("10:22AM INF 0 commits scanned.\nscan completed in 14.4ms\nno leaks found");
    }

    /// <remarks>Refer to "scan - gitleaks installed and raises error"</remarks>
    [Fact]
    public void TestScanGitleaksInstalledAndRaisesError()
    {
        GitleaksHelper.RunCommand = arg => arg == "version" ? (0, "8.18.4") : (1, "leak: API_KEY=abcd1234\n");

        int exitCode = RootCommand.Parse(["scan"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("leak: API_KEY=abcd1234");
    }
}
