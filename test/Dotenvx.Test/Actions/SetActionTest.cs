using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Moq;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/set.test.js</remarks>
public class SetActionTest : EnvironmentTestBase
{
    public SetActionTest()
    {
        RootCommand = new LoggingRootCommand { new SetAction(MockLogger) };
        // File.WriteAllText(".env.keys", ...); // Removed as we will mock file system or Sets service
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "set - no changes"</remarks>
    [Fact]
    public void TestSetNoChanges()
    {
        const string src =
            """
            #/-------------------[DOTENV_PUBLIC_KEY]--------------------/
            #/            public-key encryption for .env files          /
            #/       [how it works](https://dotenvx.com/encryption)     /
            #/----------------------------------------------------------/
            DOTENV_PUBLIC_KEY="03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba"

            # .env
            HELLO="encrypted:BJU61uy0qwXlfUDntOFnsRsA9YmP2s4QURmfyFiJv9PhiBtsYPq09LIBEIBFqEUxS0XEyOO0N3k2n5pn8+GWCX3Gi9bid5I1dKLMQOy9g8qicJQT5bWmfNu5+Eyiq53+zhPrXDkd"
            """;

        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs =
            [
                new ProcessedSet
                {
                    Key = "HELLO",
                    Value = "World",
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvSrc = src,
                    Changed = false
                }
            ],
            ChangedFilePaths = [],
            UnchangedFilePaths = [".env"]
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("no changes (.env)");
    }

    /// <remarks>Refer to "set - changes"</remarks>
    [Fact]
    public void TestSetChanges()
    {
        Fsx.WriteFileX = (_, _) => { };
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs =
            [
                new ProcessedSet
                {
                    Key = "HELLO",
                    Value = "World",
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvSrc = "HELLO=World",
                    Changed = true,
                    PrivateKeyAdded = false
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO with encryption (.env)");
    }

    /// <remarks>Refer to "set - no changes and no unchanged"</remarks>
    [Fact]
    public void TestSetNoChangesAndNoUnchanged()
    {
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
    }

    /// <remarks>Refer to "set - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestSetMissingEnvFile()
    {
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Throws(new DotenvxException("MISSING_ENV_FILE",
            $"[MISSING_ENV_FILE] missing .env file ({Path.GetFullPath(".env")})",
            "? add one with [echo \"HELLO=value\" > .env] and re-run [dotenvx set]"));
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.ErrorMessages.ShouldContain($"[MISSING_ENV_FILE] missing .env file ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("? add one with [echo \"HELLO=value\" > .env] and re-run [dotenvx set]");
    }

    /// <remarks>Refer to "set - OTHER_ERROR"</remarks>
    [Fact]
    public void TestSetOtherError()
    {
        Fsx.WriteFileX = (_, _) => { };
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs =
            [
                new ProcessedSet
                {
                    Type = EnvType.EnvFile,
                    Key = "HELLO",
                    Value = "World",
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvSrc = "HELLO=World",
                    Error = new DotenvxException("OTHER_ERROR", "Mock Error", "some help")
                }
            ],
            ChangedFilePaths = [],
            UnchangedFilePaths = [".env"]
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("no changes (.env)");
        MockLogger.WarningMessages.ShouldContain("Mock Error");
        MockLogger.InfoMessages.ShouldContain("some help");
    }

    /// <remarks>Refer to "set - changes --plain"</remarks>
    [Fact]
    public void TestSetChangesPlain()
    {
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs =
            [
                new ProcessedSet
                {
                    Key = "HELLO",
                    Value = "World",
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvSrc = "HELLO=World",
                    Changed = true
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World", "--plain"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO (.env)");
    }

    /// <remarks>Refer to "set - privateKeyAdded"</remarks>
    [Fact]
    public void TestSetPrivateKeyAdded()
    {
        Fsx.WriteFileX = (_, _) => { };
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs =
            [
                new ProcessedSet
                {
                    Type = EnvType.EnvFile,
                    Key = "HELLO",
                    Value = "World",
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvKeysFilePath = ".env.keys",
                    EnvSrc = "HELLO=World",
                    PrivateKeyAdded = true,
                    PrivateKeyName = "DOTENV_PRIVATE_KEY",
                    PrivateKey = "1234"
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO with encryption (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
        MockLogger.InfoMessages.ShouldContain(
            @"⮕  next run [DOTENV_PRIVATE_KEY='1234' dotenvx get HELLO] to test decryption locally");
    }

    /// <remarks>Refer to "set - privateKeyAdded and not ignoring .env.keys"</remarks>
    [Fact]
    public void TestSetPrivateKeyAddedAndNotIgnoringDotenvKeys()
    {
        // Assuming the default mock environment doesn't have .env.keys ignored.
        Fsx.WriteFileX = (_, _) => { };
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs =
            [
                new ProcessedSet
                {
                    Type = EnvType.EnvFile,
                    Key = "HELLO",
                    Value = "World",
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvKeysFilePath = ".env.keys",
                    EnvSrc = "HELLO=World",
                    PrivateKeyAdded = true,
                    PrivateKeyName = "DOTENV_PRIVATE_KEY",
                    PrivateKey = "1234"
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO with encryption (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
        MockLogger.InfoMessages.ShouldContain(
            "⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
        MockLogger.InfoMessages.ShouldContain(
            @"⮕  next run [DOTENV_PRIVATE_KEY='1234' dotenvx get HELLO] to test decryption locally");
    }

    /// <remarks>Refer to "set - catch error"</remarks>
    [Fact]
    public void TestSetCatchError()
    {
        Mock<Sets> mockSets = new();
        mockSets.Setup(s => s.Run()).Throws(new DotenvxException("500", "Mock Error", "Mock Help"));
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.InfoMessages.ShouldContain("Mock Help");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: 500");
    }
}