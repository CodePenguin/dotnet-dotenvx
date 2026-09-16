using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/rotate.test.js</remarks>
public class RotateActionTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;

    public RotateActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        Fsx.WriteFileX = (_, _) => { };
        RootCommand = new LoggingRootCommand { new RotateAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    /// <remarks>Refer to "rotate - nothing"</remarks>
    [Fact]
    public void TestRotateNothing()
    {
        Rotate mockRotate = Substitute.For<Rotate>();
        mockRotate.Run().Returns(new RotateResult
        {
            ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Rotate.Factory = (_, _, _, _) => mockRotate;

        ParseResult parseResult = RootCommand.Parse(["rotate"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
    }

    /// <remarks>Refer to "rotate - --stdout"</remarks>
    [Fact]
    public void TestRotateStdout()
    {
        Rotate mockRotate = Substitute.For<Rotate>();
        mockRotate.Run().Returns(new RotateResult
        {
            ProcessedEnvs =
            [
                new RotateProcessedEnv
                {
                    Keys = [],
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvSrc = "HELLO=\"encrypted:1234\"",
                    EnvKeysSrc = "DOTENV_PRIVATE_KEY=previous,newPrivateKey",
                    EnvKeysFilePath = "",
                    Changed = true,
                    PrivateKeyAdded = true,
                    PrivateKey = "newPrivateKey",
                    PrivateKeyName = "DOTENV_PRIVATE_KEY"
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Rotate.Factory = (_, _, _, _) => mockRotate;

        ParseResult parseResult = RootCommand.Parse(["rotate", "--stdout"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldContain("HELLO=\"");
        output.ShouldContain("DOTENV_PRIVATE_KEY=");
    }

    /// <remarks>Refer to "rotate - .env with changes"</remarks>
    [Fact]
    public void TestRotateEnvWithChanges()
    {
        Rotate mockRotate = Substitute.For<Rotate>();
        mockRotate.Run().Returns(new RotateResult
        {
            ProcessedEnvs =
            [
                new RotateProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    EnvFilePath = ".env",
                    FilePath = ".env",
                    Changed = true,
                    EnvSrc = "HELLO=\"encrypted:1234\"",
                    EnvKeysSrc = "DOTENV_PRIVATE_KEY=previous,newPrivateKey",
                    EnvKeysFilePath = "",
                    Keys = [],
                    PrivateKeyAdded = true,
                    PrivateKey = "newPrivateKey",
                    PrivateKeyName = "DOTENV_PRIVATE_KEY"
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Rotate.Factory = (_, _, _, _) => mockRotate;

        ParseResult parseResult = RootCommand.Parse(["rotate"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("✔ rotated (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
    }

    /// <remarks>Refer to "rotate - .env with changes and privateKeyAdded but not ignoring .env.keys"</remarks>
    [Fact]
    public void TestRotateEnvWithChangesAndPrivateKeyAddedButNotIgnoringEnvKeys()
    {
        Rotate mockRotate = Substitute.For<Rotate>();
        mockRotate.Run().Returns(new RotateResult
        {
            ProcessedEnvs =
            [
                new RotateProcessedEnv
                {
                    Keys = [],
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvSrc = "HELLO=\"encrypted:1234\"",
                    EnvKeysSrc = "DOTENV_PRIVATE_KEY=previous,newPrivateKey",
                    EnvKeysFilePath = "",
                    Changed = true,
                    PrivateKeyAdded = true,
                    PrivateKey = "newPrivateKey",
                    PrivateKeyName = "DOTENV_PRIVATE_KEY"
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Rotate.Factory = (_, _, _, _) => mockRotate;

        ParseResult parseResult = RootCommand.Parse(["rotate"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("✔ rotated (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
        MockLogger.InfoMessages.ShouldContain(
            "⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
    }

    /// <remarks>Refer to "rotate - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestRotateMissingEnvFile()
    {
        Rotate mockRotate = Substitute.For<Rotate>();
        mockRotate.Run().Returns(new RotateResult
        {
            ProcessedEnvs =
            [
                new RotateProcessedEnv
                {
                    Keys = [],
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvSrc = "HELLO=\"encrypted:1234\"",
                    EnvKeysSrc = "",
                    EnvKeysFilePath = "",
                    Changed = true,
                    PrivateKeyAdded = false,
                    Error = new DotenvxException("MISSING_ENV_FILE", "Mock Error")
                }
            ],
            ChangedFilePaths = [],
            UnchangedFilePaths = []
        });
        Rotate.Factory = (_, _, _, _) => mockRotate;

        ParseResult parseResult = RootCommand.Parse(["rotate"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.WarningMessages.ShouldContain(s => s.Contains("Mock Error"));
    }

    /// <remarks>Refer to "rotate - OTHER_ERROR"</remarks>
    [Fact]
    public void TestRotateOtherError()
    {
        Rotate mockRotate = Substitute.For<Rotate>();
        DotenvxException error = new("OTHER_ERROR", "Mock Error", "some help");
        mockRotate.Run().Returns(new RotateResult
        {
            ProcessedEnvs =
            [
                new RotateProcessedEnv
                {
                    Keys = [],
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    EnvFilePath = ".env",
                    EnvSrc = "HELLO=\"encrypted:1234\"",
                    EnvKeysSrc = "",
                    EnvKeysFilePath = "",
                    Changed = true,
                    PrivateKeyAdded = false,
                    Error = error
                }
            ],
            ChangedFilePaths = [],
            UnchangedFilePaths = []
        });
        Rotate.Factory = (_, _, _, _) => mockRotate;

        ParseResult parseResult = RootCommand.Parse(["rotate"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.WarningMessages.ShouldContain(s => s.Contains("Mock Error"));
        MockLogger.InfoMessages.ShouldContain("some help");
    }
}