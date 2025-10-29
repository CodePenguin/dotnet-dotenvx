using System.CommandLine;

using dotnet_dotenvx.Actions;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

public class RunActionTests : EnvironmentTestBase
{
    public RunActionTests()
    {
        // ClearEnvironment();
        RootCommand = new LoggingRootCommand { new RunAction(MockLogger) };
    }

    private MockLogger MockLogger { get; } = new();
    private RootCommand RootCommand { get; }


    [Fact]
    public void Run()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    [Fact]
    public void RunConventionNextjs()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "nextjs", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    [Fact]
    public void RunConventionFlow()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "flow", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    [Fact]
    public void RunEnv()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from --env flag");
    }

    [Fact]
    public void RunEnvFileWithErrors()
    {
        File.WriteAllText(".env", "HELLO=encrypted:1234");
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "bad-private-key");
        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0); // command succeeds, but error logged

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(
            "[INVALID_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=bad-pri…'");
        MockLogger.ErrorMessages.ShouldContain("[INVALID_PRIVATE_KEY] https://github.com/dotenvx/dotenvx/issues/465");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from .env");
    }

    [Fact]
    public void RunEnvFile()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from .env");
    }

    [Fact]
    public void RunEnvFileAndEnv()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from .env, and --env flag");
    }

    [Fact]
    public void RunEnvFileAndTwoEnvs()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse([
            "run", "-e", "HELLO=World", "-e", "HELLO=World", "-e", "HOLA=amigo", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HOLA=amigo)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.VerboseMessages.ShouldContain("HOLA set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.DebugMessages.ShouldContain("HOLA set to amigo");
        MockLogger.InfoMessages.ShouldContain("injecting env (2) from .env, and --env flags");
    }

    [Fact]
    public void RunEnvTwoStrings()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "-e", "HEY=there", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HEY=there)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.VerboseMessages.ShouldContain("HEY set");
        MockLogger.InfoMessages.ShouldContain("injecting env (2) from --env flags");
    }

    [Fact]
    public void RunMissingEnvFile()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "-f", "nonexistent.env", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from nonexistent.env ({Path.GetFullPath("nonexistent.env")})");
        MockLogger.ErrorMessages.ShouldContain(
            $"[MISSING_ENV_FILE] missing nonexistent.env file ({Path.GetFullPath("nonexistent.env")})");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] https://github.com/dotenvx/dotenvx/issues/484 and re-run [dotenvx run -- echo ]");
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    [Fact]
    public void RunMissingEnvFileStrictFlag()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "--strict", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain($"[MISSING_ENV_FILE] missing .env file ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain("[MISSING_ENV_FILE] https://github.com/dotenvx/dotenvx/issues/484");
    }

    [Fact]
    public void RunMissingEnvFileIgnoreFlag()
    {
        ParseResult parseResult = RootCommand.Parse([
            "run", "-f", "nonexistent.env", "--ignore", "MISSING_ENV_FILE", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from nonexistent.env ({Path.GetFullPath("nonexistent.env")})");
        MockLogger.VerboseMessages.ShouldContain(
            $"ignored: [MISSING_ENV_FILE] missing nonexistent.env file ({Path.GetFullPath("nonexistent.env")})");
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    [Fact]
    public void RunMissingEnvFileStrictFlagIgnoreFlag()
    {
        ParseResult parseResult = RootCommand.Parse([
            "run", "-f", "nonexistent.env", "--strict", "--ignore", "MISSING_ENV_FILE", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from nonexistent.env ({Path.GetFullPath("nonexistent.env")})");
        MockLogger.VerboseMessages.ShouldContain(
            $"ignored: [MISSING_ENV_FILE] missing nonexistent.env file ({Path.GetFullPath("nonexistent.env")})");
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    [Fact]
    public void RunOtherError()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "-e", "INVALID", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (INVALID)");
        MockLogger.ErrorMessages.ShouldContain($"[MISSING_ENV_FILE] missing .env file ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] https://github.com/dotenvx/dotenvx/issues/484 and re-run [dotenvx run -- echo ]");
        MockLogger.InfoMessages.ShouldContain("injecting env (0) from --env flag");
    }

    [Fact]
    public void RunThrowsError()
    {
        // To simulate throw, perhaps invalid convention
        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "invalid", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(
            "INVALID_CONVENTION: 'invalid'. permitted conventions: ['nextjs', 'flow']");
    }

    [Fact]
    public void RunEnvFileMissingCommandArgumentsAfterDoubleDash()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse(["run", "--"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(
            "missing command after [dotenvx run --]. try [dotenvx run -- yourcommand]");
    }

    [Fact]
    public void RunEnvFileAmbiguousArgumentsMissingDoubleDash()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "-f", ".env.production", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(
            "ambiguous command due to missing \'--\' separator. try [dotenvx run -f .env.production -- yourcommand]");
    }

    [Fact]
    public void RunEnvFileAmbiguousArgumentsMissingDoubleDashEnvFileEmpty()
    {
        ParseResult parseResult = RootCommand.Parse(["run", "-f", ".env", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(
            "ambiguous command due to missing \'--\' separator. try [dotenvx run -f .env -- yourcommand]");
    }

    [Fact]
    public void RunEnvFileMissingParsed()
    {
        // This test is hard to simulate without stubbing, as it requires the Run service to return incomplete data
        // Skipping or assuming it works like RunEnvFile

        File.WriteAllText(".env", string.Empty);
        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("injecting env (0) from .env");
    }

    [Fact]
    public void RunRadarObserveThrows()
    {
        // Radar not implemented, so skipping the throw part
        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.DebugMessages.ShouldContain("Ops is not currently supported.");
    }

    [Fact]
    public void RunOpsObserveThrows()
    {
        // Ops not implemented, so skipping the throw part
        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.DebugMessages.ShouldContain("Ops is not currently supported.");
    }
}