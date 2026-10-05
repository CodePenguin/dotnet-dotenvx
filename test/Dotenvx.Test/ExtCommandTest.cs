using System.CommandLine;
using System.CommandLine.Parsing;
using System.Collections.Generic;

using Dotenvx;
using Dotenvx.Interop.DotenvxJs.Helpers;
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
            new ArmorCommand(MockLogger),
            new CurlAction(MockLogger),
            new DecryptAction(MockLogger),
            new DelAction(MockLogger),
            new DoctorAction(MockLogger),
            new EncryptAction(MockLogger),
            new ExtCommand(
                new GenexampleAction(MockLogger), new GitignoreAction(MockLogger), new LsAction(MockLogger),
                new PrebuildAction(MockLogger), new PrecommitAction(MockLogger), new ScanAction(MockLogger),
                MockLogger),
            new GenexampleAction(MockLogger),
            new GetAction(MockLogger),
            new GitignoreAction(MockLogger),
            new global::Dotenvx.Actions.HelpAction(),
            new KeyPairAction(MockLogger),
            new LockCommand(MockLogger),
            new LoginAction(MockLogger),
            new LogoutAction(MockLogger),
            new LsAction(MockLogger),
            new NativeCommand(MockLogger),
            new PrebuildAction(MockLogger),
            new PrecommitAction(MockLogger),
            new RotateAction(MockLogger),
            new RunAction(MockLogger),
            new SetAction(MockLogger),
            new UpdateAction(),
            new ValidateAction(MockLogger));
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
        output.ShouldContain("scan");
    }

    /// <remarks>
    /// Confirms scan's registration asymmetry vs every other ext subcommand - it's ext-only,
    /// no top-level `dotenvx scan` alias exists upstream, so parsing the bare top-level form
    /// must fail while `ext scan` succeeds.
    /// </remarks>
    [Fact]
    public void TestScanIsExtOnlyNotTopLevel()
    {
        IReadOnlyList<ParseError> topLevelErrors = RootCommand.Parse(["scan"]).Errors;
        topLevelErrors.ShouldNotBeEmpty();
    }

    /// <remarks>Refer to "ext dynamic action forwards to executeExtension"</remarks>
    [Fact]
    public void TestExtDynamicActionForwardsToExecuteExtension()
    {
        var spawned = new List<(string Command, string[] Args)>();
        ExecuteDynamic.SpawnCommand = (command, args, _) =>
        {
            spawned.Add((command, args));

            return new SpawnResult(0, false);
        };

        RootCommand.Parse(["ext", "bogus", "--json"]).Invoke().ShouldBe(0);

        spawned.Single().Command.ShouldBe("dotenvx-ext-bogus");
        spawned.Single().Args.ShouldBe(["--json"]);
    }

    /// <remarks>
    /// Refer to "ext subcommands lazy-load handlers and preserve this context". Upstream lazy-loads each handler with
    /// <c>require</c> and calls it on the commander command; the equivalent here is that every registered subcommand is its own
    /// action instance, present on the ext command in ext.js's registration order.
    /// </remarks>
    [Fact]
    public void TestExtSubcommandsLazyLoadHandlersAndPreserveThisContext()
    {
        Command ext = RootCommand.Subcommands.Single(command => command.Name == "ext");

        ext.Subcommands.Select(command => command.Name).ShouldBe(
            ["ls", "genexample", "gitignore", "prebuild", "precommit", "scan"]);
    }

    /// <remarks>Refer to "ext helpInformation removes dynamic section"</remarks>
    [Fact]
    public void TestExtHelpInformationRemovesDynamicSection()
    {
        RootCommand.Parse(["ext", "--help"]).Invoke().ShouldBe(0);

        _consoleOutput.ToString().ShouldNotContain("dynamic ext command");
    }
}
