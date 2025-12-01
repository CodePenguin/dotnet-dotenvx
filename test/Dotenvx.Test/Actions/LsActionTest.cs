using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;

using Moq;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/ls.test.js</remarks>
public class LsActionTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleOutput = new();
    private readonly TextWriter _originalOut = Console.Out;

    public LsActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new LsAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    /// <remarks>Refer to "ls calls main.ls"</remarks>
    [Fact]
    public void TestLsCallsMainLs()
    {
        Mock<Ls> mockLs = new();
        mockLs.Setup(l => l.Run()).Returns([".env"]);
        Ls.Factory = (_, _, _) => mockLs.Object;

        ParseResult parseResult = RootCommand.Parse(["ls"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldNotBeNull();
    }
}