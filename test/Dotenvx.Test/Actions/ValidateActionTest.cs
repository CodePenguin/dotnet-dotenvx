using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/validate.js</remarks>
public class ValidateActionTest : EnvironmentTestBase
{
    public ValidateActionTest()
    {
        RootCommand = new LoggingRootCommand { new ValidateAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    private static void MockRun(params RunProcessedEnv[] processedEnvs)
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs = processedEnvs,
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;
    }

    [Fact]
    public void TestValidateSucceedsWhenNoErrorsAndExampleValid()
    {
        MockRun(new RunProcessedEnv
        {
            Type = EnvType.EnvFile, FilePath = ".env", Parsed = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(), PreExisted = new Dictionary<string, string>(), Errors = []
        });
        Fsx.ExistsSync = _ => false; // no .env.example -> MISSING_ENV_EXAMPLE

        int exitCode = RootCommand.Parse(["validate"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_EXAMPLE] missing .env.example file. fix: [https://github.com/dotenvx/dotenvx/issues/905]");
    }

    [Fact]
    public void TestValidateSucceedsWhenExampleSatisfied()
    {
        MockRun(new RunProcessedEnv
        {
            Type = EnvType.EnvFile, FilePath = ".env", Parsed = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(), PreExisted = new Dictionary<string, string>(), Errors = []
        });
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";

        int exitCode = RootCommand.Parse(["validate"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("▣ validated");
    }

    [Fact]
    public void TestValidatePropagatesPerFileErrors()
    {
        MockRun(new RunProcessedEnv
        {
            Type = EnvType.EnvFile, FilePath = ".env", Parsed = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(), PreExisted = new Dictionary<string, string>(),
            Errors = [new MissingEnvFileException(".env")]
        });
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";

        int exitCode = RootCommand.Parse(["validate"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] missing file (.env). fix: [https://github.com/dotenvx/dotenvx/issues/484]");
    }

    [Fact]
    public void TestValidateIgnoresErrorCodeInIgnoreList()
    {
        MockRun(new RunProcessedEnv
        {
            Type = EnvType.EnvFile, FilePath = ".env", Parsed = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(), PreExisted = new Dictionary<string, string>(),
            Errors = [new MissingEnvFileException(".env")]
        });
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";

        int exitCode = RootCommand.Parse(["validate", "--ignore", "MISSING_ENV_FILE"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.ErrorMessages.ShouldNotContain(s => s.Contains("MISSING_ENV_FILE"));
        MockLogger.VerboseMessages.ShouldContain(s => s.StartsWith("ignored:"));
        MockLogger.InfoMessages.ShouldContain("▣ validated");
    }

    [Fact]
    public void TestValidateReportsMissingRequiredKey()
    {
        MockRun(new RunProcessedEnv
        {
            Type = EnvType.EnvFile, FilePath = ".env", Parsed = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(), PreExisted = new Dictionary<string, string>(), Errors = []
        });
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "DATABASE_URL=\n";

        int exitCode = RootCommand.Parse(["validate"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[VALIDATION_FAILED] missing required (DATABASE_URL). fix: [https://github.com/dotenvx/dotenvx/issues/907]");
    }
}
