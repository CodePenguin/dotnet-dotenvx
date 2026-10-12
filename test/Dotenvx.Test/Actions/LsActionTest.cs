using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/ls.test.js</remarks>
public class LsActionTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleError = new() { NewLine = "\n" };
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalError = Console.Error;
    private readonly TextWriter _originalOut = Console.Out;

    public LsActionTest()
    {
        Console.SetOut(_consoleOutput);
        Console.SetError(_consoleError);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new LsAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }

    /// <remarks>Refer to "ls calls main.ls"</remarks>
    [Fact]
    public void TestLsCallsMainLs()
    {
        Ls mockLs = Substitute.For<Ls>();
        mockLs.Run().Returns([".env"]);
        Ls.Factory = (_, _, _) => mockLs;

        ParseResult parseResult = RootCommand.Parse(["ls"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldNotBeNull();
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-25 fix. ls.js prints this summary line
    /// via a raw console.error() call that bypasses the logger entirely - this port never printed
    /// it at all before. The "directories scanned" count walks the REAL filesystem directly
    /// (rather than going through the mocked Ls.Factory), so this runs in an isolated, empty temp
    /// directory rather than EnvironmentTestBase's default CWD (the test project's own build
    /// output directory, which has dozens of incidental subdirectories of its own).
    /// </remarks>
    [Fact]
    public void TestLsPrintsFoundSummaryLineToStderr()
    {
        DirectoryInfo scratch = Directory.CreateTempSubdirectory("dotenvx-ls-test-");
        Environment.CurrentDirectory = scratch.FullName;
        try
        {
            Ls mockLs = Substitute.For<Ls>();
            mockLs.Run().Returns([".env"]);
            Ls.Factory = (_, _, _) => mockLs;

            ParseResult parseResult = RootCommand.Parse(["ls"]);
            int exitCode = parseResult.Invoke();

            exitCode.ShouldBe(0);
            string error = _consoleError.ToString();
            error.ShouldBe("▣ found 1 .env file across 1 directory of 1 scanned in 0s\n");
        }
        finally
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            Directory.Delete(scratch.FullName, true);
        }
    }

    /// <remarks>
    /// Not from a JS test - companion regression test confirming plural wording when there are
    /// zero or multiple matches.
    /// </remarks>
    [Fact]
    public void TestLsPrintsPluralSummaryLineForMultipleFiles()
    {
        DirectoryInfo scratch = Directory.CreateTempSubdirectory("dotenvx-ls-test-");
        Environment.CurrentDirectory = scratch.FullName;
        try
        {
            Ls mockLs = Substitute.For<Ls>();
            mockLs.Run().Returns(["a/.env", "b/.env"]);
            Ls.Factory = (_, _, _) => mockLs;

            ParseResult parseResult = RootCommand.Parse(["ls"]);
            int exitCode = parseResult.Invoke();

            exitCode.ShouldBe(0);
            string error = _consoleError.ToString();
            error.ShouldBe("▣ found 2 .env files across 2 directories of 1 scanned in 0s\n");
        }
        finally
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            Directory.Delete(scratch.FullName, true);
        }
    }

    /// <remarks>
    /// Refer to "ls does not print a summary in quiet mode". The upstream tests "ls traverses with a spinner and stops it
    /// before output" and "ls stops the spinner and reports traversal errors" are about the TTY spinner, which this port
    /// does not have; their non-spinner assertions (summary line to stderr, not stdout) are covered by
    /// <see cref="TestLsPrintsFoundSummaryLineToStderr" />.
    /// </remarks>
    [Fact]
    public void TestLsDoesNotPrintASummaryInQuietMode()
    {
        Ls mockLs = Substitute.For<Ls>();
        mockLs.Run().Returns([".env"]);
        Ls.Factory = (_, _, _) => mockLs;

        int exitCode = RootCommand.Parse(["ls", "--quiet"]).Invoke();

        exitCode.ShouldBe(0);
        _consoleError.ToString().ShouldBeEmpty();
    }

    /// <remarks>Refer to "ls prints absolute filepaths as json"</remarks>
    [Fact]
    public void TestLsPrintsAbsoluteFilepathsAsJson()
    {
        DirectoryInfo scratch = Directory.CreateTempSubdirectory("dotenvx-ls-test-");
        Environment.CurrentDirectory = scratch.FullName;
        try
        {
            Ls mockLs = Substitute.For<Ls>();
            mockLs.Run().Returns([".env", "packages/example/.env"]);
            Ls.Factory = (_, _, _) => mockLs;

            int exitCode = RootCommand.Parse(["ls", "--json", scratch.FullName]).Invoke();

            exitCode.ShouldBe(0);
            string[]? printed = System.Text.Json.JsonSerializer.Deserialize<string[]>(_consoleOutput.ToString());
            printed.ShouldBe([
                Path.Combine(scratch.FullName, ".env"),
                Path.GetFullPath(Path.Combine(scratch.FullName, "packages/example/.env"))
            ]);
            MockLogger.InfoMessages.ShouldBeEmpty(); // does not print the tree
            _consoleError.ToString().ShouldStartWith("▣ found 2 .env files");
        }
        finally
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            Directory.Delete(scratch.FullName, true);
        }
    }
}
