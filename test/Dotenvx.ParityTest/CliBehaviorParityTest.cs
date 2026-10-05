using System.Text.RegularExpressions;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - found by the differential sweep (scripts/parity-sweep.js): top-level CLI behavior of
/// dotenvx.js itself (no arguments, unknown command) and `ls` on a directory that does not exist.
/// </remarks>
public partial class CliBehaviorParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [GeneratedRegex(@"scanned in \d+s")]
    private static partial Regex ElapsedRegex();

    /// <remarks>commander prints the help and exits 1 when dotenvx is run with no arguments.</remarks>
    [Fact]
    public async Task NoArgumentsPrintsHelpAndExitsOne()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null);

        node.ExitCode.ShouldBe(1);
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        node.StandardOutput.ShouldContain("Commands:");
        dotnet.StandardOutput.ShouldContain("Commands:");
    }

    /// <remarks>dotenvx.js handles `command:*` by printing this line to stdout and exiting 0.</remarks>
    [Fact]
    public async Task UnknownCommandPrintsErrorToStdoutAndExitsZero()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "bogus");

        node.StandardOutput.ShouldBe("error: unknown command 'bogus'\n");
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
    }

    /// <remarks>Fdir crawls a missing directory (or a plain file given as the directory) as empty.</remarks>
    [Theory]
    [InlineData("nonexistent")]
    [InlineData(".env")]
    public async Task LsOfAMissingDirectoryReportsZeroFiles(string directory)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => WriteFile(".env", "A=1\n"), null, "ls", directory);

        node.ExitCode.ShouldBe(0);
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        ElapsedRegex().Replace(dotnet.StandardError, "scanned in Ns")
            .ShouldBe(ElapsedRegex().Replace(node.StandardError, "scanned in Ns"));
    }

    /// <remarks>Not from a JS test - doctor/update/help [command] compared against the real CLI (Phase A).</remarks>
    [Theory]
    [InlineData("doctor")]
    [InlineData("doctor", "sub")]
    [InlineData("doctor", "missing")]
    [InlineData("doctor", "-q")]
    [InlineData("update")]
    public async Task PhaseACommandsMatchNode(params string[] args)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() =>
        {
            WriteFile("app.py", "load_dotenv()" + "\n");
            WriteFile("sub/index.js", "require('dotenv').config()" + "\n" + "dotenv.config()" + "\n");
            WriteFile("node_modules/x.js", "dotenv.config()" + "\n");
        }, null, args);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
    }

    /// <remarks>Not from a JS test - help x exits 0 like commander; the text is System.CommandLine's help renderer (accepted difference).</remarks>
    [Theory]
    [InlineData("help")]
    [InlineData("help", "get")]
    [InlineData("help", "bogus")]
    public async Task HelpCommandExitsLikeNode(params string[] args)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, args);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldContain(args.Length > 1 && args[1] == "get" ? "get" : "Commands:");
    }
}
