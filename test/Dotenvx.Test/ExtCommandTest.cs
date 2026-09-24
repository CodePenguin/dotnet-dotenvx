using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/ext.js. Confirms the nested-Command wiring:
/// `dotenvx ext ls` dispatches identically to `dotenvx ls`, and `ext` is hidden from the root's
/// own --help listing without breaking `ext --help`'s own rendering (see DotenvxRootCommand's
/// FilteringHelpAction doc comment for why a plain Command.Hidden = true doesn't work here -
/// found and fixed via this test, 2026-09-23: HelpBuilder.Write early-returns with NO output at
/// all when the command being asked about is itself Hidden, not just when listed by a parent).
/// Uses the real DotenvxRootCommand (not a parallel test harness) so the actual FilteringHelpAction
/// wiring is genuinely exercised, not just a hand-rolled approximation of it.
/// </remarks>
public class ExtCommandTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;

    public ExtCommandTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new DotenvxRootCommand(
            new DecryptAction(MockLogger),
            new DelAction(MockLogger),
            new EncryptAction(MockLogger),
            new ExtCommand(new GitignoreAction(MockLogger), new LsAction(MockLogger)),
            new GetAction(MockLogger),
            new GitignoreAction(MockLogger),
            new KeyPairAction(MockLogger),
            new LsAction(MockLogger),
            new RotateAction(MockLogger),
            new RunAction(MockLogger),
            new SetAction(MockLogger));
    }

    private DotenvxRootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    [Fact]
    public void TestExtLsDispatchesToSameLsService()
    {
        Ls mockLs = Substitute.For<Ls>();
        mockLs.Run().Returns([".env"]);
        Ls.Factory = (_, _, _) => mockLs;

        int exitCode = RootCommand.Parse(["ext", "ls"]).Invoke();

        exitCode.ShouldBe(0);
        mockLs.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain(s => s.Contains(".env"));
    }

    [Fact]
    public void TestRootHelpDoesNotListExt()
    {
        int exitCode = RootCommand.Parse(["--help"]).Invoke();

        exitCode.ShouldBe(0);
        _consoleOutput.ToString().ShouldNotContain("extensions");
    }

    [Fact]
    public void TestExtHelpListsItsOwnSubcommands()
    {
        int exitCode = RootCommand.Parse(["ext", "--help"]).Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldContain("extensions");
        output.ShouldContain("ls");
    }
}
