using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/genexample.js</remarks>
public class GenexampleActionTest : EnvironmentTestBase
{
    public GenexampleActionTest()
    {
        RootCommand = new LoggingRootCommand { new GenexampleAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "genexample calls Genexample.run"</remarks>
    [Fact]
    public void TestGenexampleGeneratedLogsSuccess()
    {
        Fsx.WriteFileX = (_, _) => { };
        Genexample mockGenexample = Substitute.For<Genexample>();
        mockGenexample.Run().Returns(new GenexampleResult
        {
            EnvExampleFile = "HELLO=\"\"", EnvFile = [".env"], ExampleFilepath = ".env.example",
            AddedKeys = ["HELLO"], Injected = new Dictionary<string, string> { { "HELLO", "" } },
            Existed = new Dictionary<string, string>()
        });
        Genexample.Factory = (_, _) => mockGenexample;

        int exitCode = RootCommand.Parse(["genexample", "."]).Invoke();

        exitCode.ShouldBe(0);
        mockGenexample.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("▣ generated (.env.example)");
    }

    /// <remarks>Refer to "genexample calls Genexample.run (no addedKeys changes)"</remarks>
    [Fact]
    public void TestGenexampleNoChangeLogsInfo()
    {
        Fsx.WriteFileX = (_, _) => { };
        Genexample mockGenexample = Substitute.For<Genexample>();
        mockGenexample.Run().Returns(new GenexampleResult
        {
            EnvExampleFile = "HELLO=''", EnvFile = [".env"], ExampleFilepath = ".env.example",
            AddedKeys = [], Injected = new Dictionary<string, string>(),
            Existed = new Dictionary<string, string> { { "HELLO", "" } }
        });
        Genexample.Factory = (_, _) => mockGenexample;

        int exitCode = RootCommand.Parse(["genexample"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("○ no change (.env.example)");
    }

    /// <remarks>Refer to "genexample calls Genexample.run (other error)"</remarks>
    [Fact]
    public void TestGenexampleOtherErrorExitsOne()
    {
        Genexample mockGenexample = Substitute.For<Genexample>();
        mockGenexample.Run().Throws(new Exception("other error"));
        Genexample.Factory = (_, _) => mockGenexample;

        int exitCode = RootCommand.Parse(["genexample"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("other error");
    }

    /// <remarks>Refer to "genexample calls Genexample.run (error with code and help message)"</remarks>
    [Fact]
    public void TestGenexampleDotenvxExceptionExitsOne()
    {
        Genexample mockGenexample = Substitute.For<Genexample>();
        mockGenexample.Run().Throws(new MissingEnvFilesException());
        Genexample.Factory = (_, _) => mockGenexample;

        int exitCode = RootCommand.Parse(["genexample"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILES] no .env* files found. fix: [https://github.com/dotenvx/dotenvx/issues/760]");
    }
}
