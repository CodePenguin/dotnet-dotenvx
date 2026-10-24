using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs;

/// <remarks>Reference source: dotenvx/tests/lib/config.test.js</remarks>
public class ConfigTest : EnvironmentTestBase
{
    private const string EnvFileName = "tests/.env";
    private const string EnvLocalFileName = "tests/.env.local";

    public ConfigTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "takes array for path option"</remarks>
    [Fact]
    public void TestTakesArrayForPathOption()
    {
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to "takes two or more files in the array for path option"</remarks>
    [Fact]
    public void TestTakesTwoOrMoreFilesInTheArrayForPathOption()
    {
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvLocalFileName, EnvFileName] });

        result.Parsed["BASIC"].ShouldBe("local_basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");
    }

    /// <remarks>Refer to "does not write over keys already in process.env"</remarks>
    [Fact]
    public void TestDoesNotWriteOverKeysAlreadyInProcessEnv()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        result.Parsed["BASIC"].ShouldBe("existing");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("existing");
    }

    /// <remarks>Refer to "writes over keys already in process.env if override turned on"</remarks>
    [Fact]
    public void TestWritesOverKeysAlreadyInProcessEnvIfOverloadTrue()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName], Overload = true });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to main.js `options.overload || options.override`</remarks>
    [Fact]
    public void TestWritesOverKeysAlreadyInProcessEnvIfOverrideTrue()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName], Override = true });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to "does not write over keys already in process.env if the key has a falsy value"</remarks>
    [Fact]
    public void TestDoesNotOverrideExistingFalsyValues()
    {
        Environment.SetEnvironmentVariable("BASIC", "");
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        /*
        Node.js behavior is to not override blank values, but .NET 8.0 removed the environment variable when set
        to blank, making it appear unset and therefore overridden. Since .NET 9.0 (and net10.0, targeted here),
        the blank value is kept, matching Node.js semantics.
        https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable
        */
        const string expected = "";
        result.Parsed["BASIC"].ShouldBe(expected);
        Environment.GetEnvironmentVariable("BASIC").ShouldBe(expected);
    }

    /// <remarks>
    ///     Refer to "does write over keys already in process.env if the key has a falsy value but override is set to
    ///     true"
    /// </remarks>
    [Fact]
    public void TestDoesOverrideExistingFalsyValuesIfOverloadTrue()
    {
        Environment.SetEnvironmentVariable("BASIC", "");
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName], Overload = true });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to "can write to a different object rather than process.env"</remarks>
    [Fact]
    public void TestCanWriteToADifferentObjectThanProcessEnv()
    {
        Dictionary<string, string> myObject = [];
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName], ProcessEnv = myObject });

        result.Parsed["BASIC"].ShouldBe("basic");
        myObject["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBeNull();
    }

    /// <remarks>Refer to "returns parsed object"</remarks>
    [Fact]
    public void TestReturnsParsedObject()
    {
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        result.Error.ShouldBeNull();
        result.Parsed["BASIC"].ShouldBe("basic");
    }

    /// <remarks>Refer to "returns any errors thrown from reading file or parsing"</remarks>
    [Fact]
    public void TestReturnsErrorWhenFileMissing()
    {
        Fsx.ReadFileSync = _ => []; // for encoding check
        Fsx.ReadFileX = (_, _) => throw new Exception();

        ConfigResult result = Main.Config();

        result.Error.ShouldBeOfType<Exception>();
    }

    /// <remarks>Refer to "sets values from both .env.local and .env. first file key wins."</remarks>
    [Fact]
    public void TestSetsValuesFromBothEnvLocalAndEnvWhenBothExist()
    {
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvLocalFileName, EnvFileName] });

        result.Parsed["BASIC"].ShouldBe("local_basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");

        result.Parsed["LOCAL"].ShouldBe("local");
        Environment.GetEnvironmentVariable("LOCAL").ShouldBe("local");

        result.Parsed["SINGLE_QUOTES"].ShouldBe("single_quotes");
        Environment.GetEnvironmentVariable("SINGLE_QUOTES").ShouldBe("single_quotes");
    }

    /// <remarks>Refer to "handles inline comments"</remarks>
    [Fact]
    public void TestHandlesInlineComments()
    {
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        result.Parsed["INLINE_COMMENTS"].ShouldBe("inline comments");
        Environment.GetEnvironmentVariable("INLINE_COMMENTS").ShouldBe("inline comments");
    }

    /// <remarks>Refer to "takes string for path option"</remarks>
    [Fact]
    public void TestTakesStringForPathOption()
    {
        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to "logs when in debug mode"</remarks>
    [Fact]
    public void TestLogsWhenInDebugMode()
    {
        Main.Config(new ConfigOptions { Debug = true, Logger = MockLogger });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Debug);
        MockLogger.DebugMessages.ShouldContain("setting log level to: debug");
    }

    /// <remarks>Refer to "logs only errors in quiet mode"</remarks>
    [Fact]
    public void TestLogsOnlyErrorsInQuietMode()
    {
        Main.Config(new ConfigOptions { Quiet = true, Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Error);
    }

    /// <remarks>Refer to "logs in verbose mode"</remarks>
    [Fact]
    public void TestLogsInVerboseMode()
    {
        Main.Config(new ConfigOptions { Verbose = true, Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Verbose);
        MockLogger.DebugMessages.ShouldContain("setting log level to: verbose");
    }

    /// <remarks>Refer to "sets specific log level and logs it"</remarks>
    [Fact]
    public void TestSetsSpecificLogLevelAndLogsIt()
    {
        Main.Config(new ConfigOptions { LogLevel = "warn", Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Warn);
        MockLogger.DebugMessages.ShouldContain("setting log level to: warn");
    }

    /// <remarks>Refer to "verbose mode overrides quiet mode"</remarks>
    [Fact]
    public void TestVerboseModeOverridesQuietMode()
    {
        Main.Config(new ConfigOptions { Quiet = true, Verbose = true, Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Verbose);
        MockLogger.DebugMessages.ShouldContain("setting log level to: verbose");
    }

    /// <remarks>Refer to "quiet mode overrides specific log level"</remarks>
    [Fact]
    public void TestQuietModeOverridesSpecificLogLevel()
    {
        Main.Config(new ConfigOptions { LogLevel = "warn", Quiet = true, Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Error);
    }

    /// <remarks>Refer to "debug mode overrides logLevel, quiet, verbose"</remarks>
    [Fact]
    public void TestDebugModeOverridesLogLevelQuietVerbose()
    {
        Main.Config(new ConfigOptions
        {
            LogLevel = "warn",
            Debug = true,
            Quiet = true,
            Verbose = true,
            Logger = MockLogger,
            Path = []
        });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Debug);
        MockLogger.DebugMessages.ShouldContain("setting log level to: debug");
    }

    /// <remarks>Refer to "logs any errors thrown from reading file or parsing when in debug mode"</remarks>
    [Fact]
    public void TestLogsAnyErrorsThrownFromReadingFileOrParsingWhenInDebugMode()
    {
        Fsx.ReadFileSync = _ => []; // for encoding check
        Fsx.ReadFileX = (_, _) => throw new Exception();
        ConfigResult result = Main.Config(new ConfigOptions { Debug = true, Logger = MockLogger });

        result.Error.ShouldBeOfType<Exception>();
    }

    /// <remarks>Refer to "takes option for path along with home directory char ~"</remarks>
    [Fact]
    public void TestTakesOptionForPathAlongWithHomeDirectoryChar()
    {
        IFsxMock mockFsx = Substitute.For<IFsxMock>();
        mockFsx.ReadFileX(Arg.Any<string>(), "utf8").Returns("BASIC=basic");
        Fsx.ReadFileX = mockFsx.ReadFileX;
        Fsx.ReadFileSync = path => Path.GetFileName(path) == "Envfile" ? throw new FileNotFoundException() : "BASIC=basic"u8.ToArray(); // for encoding check; no Envfile

        ConfigResult result = Main.Config(new ConfigOptions { Path = ["~/.env"] });

        result.Parsed["BASIC"].ShouldBe("basic");
        mockFsx.DidNotReceive().ReadFileX("~/.env", "utf8");
        mockFsx.Received(1).ReadFileX(Arg.Is<string>(s => s != "~/.env"), "utf8");
    }

    /// <remarks>Refer to "reads path with encoding, parsing output to process.env"</remarks>
    [Fact]
    public void TestReadsPathWithEncodingParsingOutputToProcessEnv()
    {
        IFsxMock mockFsx = Substitute.For<IFsxMock>();
        mockFsx.ReadFileX(Arg.Any<string>(), "utf8").Returns("BASIC=basic");
        mockFsx.ReadFileSync(Arg.Any<string>()).Returns(call => Path.GetFileName(call.Arg<string>()) == "Envfile" ? throw new FileNotFoundException() : "test=foo"u8.ToArray()); // for purpose of encoding check; no Envfile
        Fsx.ReadFileX = mockFsx.ReadFileX;
        Fsx.ReadFileSync = mockFsx.ReadFileSync;

        ConfigResult result = Main.Config(new ConfigOptions { Path = [] });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
        mockFsx.Received(1).ReadFileX(Arg.Any<string>(), "utf8");
        mockFsx.Received(1).ReadFileSync(Arg.Is<string>(s => Path.GetFileName(s) == ".env")); // encoding check
    }

    /// <remarks>Refer to "takes comma-separated string for path option"</remarks>
    [Fact]
    public void TestTakesCommaSeparatedStringForPathOption()
    {
        ConfigResult result = Main.Config(new ConfigOptions { PathString = "tests/.env.local, tests/.env" });

        result.Parsed["BASIC"].ShouldBe("local_basic");
        result.Parsed["SINGLE_QUOTES"].ShouldBe("single_quotes");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");
    }

    /// <remarks>Refer to "respects DOTENV_F"</remarks>
    [Fact]
    public void TestRespectsDotenvF()
    {
        Environment.SetEnvironmentVariable("DOTENV_F", "tests/.env.local");

        ConfigResult result = Main.Config();

        result.Parsed["BASIC"].ShouldBe("local_basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");
    }

    /// <remarks>Refer to "respects DOTENV_PATH"</remarks>
    [Fact]
    public void TestRespectsDotenvPath()
    {
        Environment.SetEnvironmentVariable("DOTENV_PATH", "tests/.env.local");

        ConfigResult result = Main.Config();

        result.Parsed["BASIC"].ShouldBe("local_basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");
    }

    /// <remarks>Refer to "path option takes precedence over configured paths"</remarks>
    [Fact]
    public void TestPathOptionTakesPrecedenceOverConfiguredPaths()
    {
        Environment.SetEnvironmentVariable("DOTENV_F", "tests/.env.local");
        Environment.SetEnvironmentVariable("DOTENV_PATH", "tests/.env.local");

        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName] });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to "sets values from both .env.local and .env. but neither is used as value existed in process.env."</remarks>
    [Fact]
    public void TestNeitherFileIsUsedAsTheValueExistedInProcessEnv()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");

        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvLocalFileName, EnvFileName] });

        // does not override process.env
        result.Parsed["BASIC"].ShouldBe("existing");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("existing");
    }

    /// <remarks>Refer to "does write over keys already in process.env if the key has a falsy value but override is set to true"</remarks>
    [Fact]
    public void TestWritesOverAnEmptyExistingValueWhenOverloadIsTrue()
    {
        Environment.SetEnvironmentVariable("BASIC", "");

        ConfigResult result = Main.Config(new ConfigOptions { Path = [EnvFileName], Overload = true });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    /// <remarks>Refer to "respects DOTENV_CONFIG_IGNORE environment variable and does not log matching error"</remarks>
    [Fact]
    public void TestRespectsDotenvConfigIgnoreAndDoesNotLogMatchingError()
    {
        const string missing = "./.env.fake";
        string missingEnvFileError = new MissingEnvFileException(missing).MessageWithHelp;

        // control path
        MockLogger control = new();
        Main.Config(new ConfigOptions { Path = [missing], Logger = control });
        control.ErrorMessages.ShouldContain(missingEnvFileError);

        // golden path, merge with existing options, and the diabolical comma shapes
        foreach (string ignoreString in new[]
                 {
                     "MISSING_ENV_FILE", "MISSING_ENV_FILE,FAKE_ERROR", "FAKE_ERROR,MISSING_ENV_FILE",
                     "FAKE_ERROR,MISSING_ENV_FILE,FAKE_ERROR_2", "MISSING_ENV_FILE,", ",MISSING_ENV_FILE",
                     ",MISSING_ENV_FILE,"
                 })
        {
            Environment.SetEnvironmentVariable("DOTENV_CONFIG_IGNORE", ignoreString);
            MockLogger logger = new();
            Main.Config(new ConfigOptions { Path = [missing], Logger = logger });
            logger.ErrorMessages.ShouldNotContain(missingEnvFileError);
        }

        Environment.SetEnvironmentVariable("DOTENV_CONFIG_IGNORE", "MISSING_ENV_FILE");
        MockLogger merged = new();
        Main.Config(new ConfigOptions { Path = [missing], Logger = merged, Ignore = ["FAKE_ERROR"] });
        merged.ErrorMessages.ShouldNotContain(missingEnvFileError);
    }

    /// <remarks>Refer to "logs only errors when DOTENV_CONFIG_QUIET=true"</remarks>
    [Fact]
    public void TestLogsOnlyErrorsWhenDotenvConfigQuietIsTrue()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_QUIET", "true");

        Main.Config(new ConfigOptions { Quiet = false, Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Error);
    }
}
