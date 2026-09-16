using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Moq;

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
        Dictionary<string, string> myObject = new();
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
        MockLogger.DebugMessages.ShouldContain("Setting log level to debug");
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
        MockLogger.DebugMessages.ShouldContain("Setting log level to verbose");
    }

    /// <remarks>Refer to "sets specific log level and logs it"</remarks>
    [Fact]
    public void TestSetsSpecificLogLevelAndLogsIt()
    {
        Main.Config(new ConfigOptions { LogLevel = "warn", Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Warn);
        MockLogger.DebugMessages.ShouldContain("Setting log level to warn");
    }

    /// <remarks>Refer to "verbose mode overrides quiet mode"</remarks>
    [Fact]
    public void TestVerboseModeOverridesQuietMode()
    {
        Main.Config(new ConfigOptions { Quiet = true, Verbose = true, Logger = MockLogger, Path = [] });

        MockLogger.SetLevelCalled.ShouldBe(DotenvxLogLevel.Verbose);
        MockLogger.DebugMessages.ShouldContain("Setting log level to verbose");
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
        MockLogger.DebugMessages.ShouldContain("Setting log level to debug");
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
        Mock<IFsxMock> mockFsx = new();
        mockFsx.Setup(x => x.ReadFileX(It.IsAny<string>(), "utf8"))
            .Returns("BASIC=basic");
        Fsx.ReadFileX = mockFsx.Object.ReadFileX;
        Fsx.ReadFileSync = _ => "BASIC=basic"u8.ToArray(); // for encoding check

        ConfigResult result = Main.Config(new ConfigOptions { Path = ["~/.env"] });

        result.Parsed["BASIC"].ShouldBe("basic");
        mockFsx.Verify(x => x.ReadFileX("~/.env", "utf8"), Times.Never);
        mockFsx.Verify(x => x.ReadFileX(It.IsNotIn("~/.env"), "utf8"), Times.Once);
    }

    /// <remarks>Refer to "reads path with encoding, parsing output to process.env"</remarks>
    [Fact]
    public void TestReadsPathWithEncodingParsingOutputToProcessEnv()
    {
        Mock<IFsxMock> mockFsx = new();
        mockFsx.Setup(x => x.ReadFileX(It.IsAny<string>(), "utf8"))
            .Returns("BASIC=basic");
        mockFsx.Setup(x => x.ReadFileSync(It.IsAny<string>()))
            .Returns("test=foo"u8.ToArray()); // for purpose of encoding check
        Fsx.ReadFileX = mockFsx.Object.ReadFileX;
        Fsx.ReadFileSync = mockFsx.Object.ReadFileSync;

        ConfigResult result = Main.Config(new ConfigOptions { Path = [] });

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
        mockFsx.Verify(x => x.ReadFileX(It.IsAny<string>(), "utf8"), Times.Once);
        mockFsx.Verify(x => x.ReadFileSync(It.IsAny<string>()), Times.Once);
    }
}