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

        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Returns(new SetsResult
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
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("○ no change (.env)");
    }

    /// <remarks>Refer to "set - changes"</remarks>
    [Fact]
    public void TestSetChanges()
    {
        Fsx.WriteFileX = (_, _) => { };
        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Returns(new SetsResult
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
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("◈ encrypted HELLO (.env)");
    }

    /// <remarks>Refer to "set - no changes and no unchanged"</remarks>
    [Fact]
    public void TestSetNoChangesAndNoUnchanged()
    {
        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Received(1).Run();
    }

    /// <remarks>
    /// Refer to "set - MISSING_ENV_FILE". Rewritten to throw the real MissingEnvFileException
    /// type (previously a hand-rolled DotenvxException with invented help text matching a
    /// per-code special case that no longer exists in SetAction.cs - it now uses the same
    /// universal MessageWithHelp pattern for every error code, matching set.js's
    /// `logger.error(processedEnv.error.messageWithHelp || processedEnv.error.message)`).
    /// </remarks>
    [Fact]
    public void TestSetMissingEnvFile()
    {
        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Throws(new MissingEnvFileException(".env"));
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockSets.Received(1).Run();
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] missing file (.env). fix: [https://github.com/dotenvx/dotenvx/issues/484]");
    }

    /// <remarks>
    /// Refer to "set - OTHER_ERROR". Verified against the real Node CLI (a generic per-file
    /// error, e.g. EISDIR, exits 1) that SetAction's errorCount fix (Task D) is correct here -
    /// this test previously locked in the same unconditional-exit-0 bug found and fixed in
    /// EncryptAction/DecryptAction.
    /// </remarks>
    [Fact]
    public void TestSetOtherError()
    {
        Fsx.WriteFileX = (_, _) => { };
        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Returns(new SetsResult
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
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("○ no change (.env)");
        MockLogger.ErrorMessages.ShouldContain("Mock Error. some help");
    }

    /// <remarks>Refer to "set - changes --plain"</remarks>
    [Fact]
    public void TestSetChangesPlain()
    {
        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Returns(new SetsResult
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
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World", "--plain"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("◇ set HELLO (.env)");
    }

    /// <remarks>Refer to "set - catch error"</remarks>
    [Fact]
    public void TestSetCatchError()
    {
        Sets mockSets = Substitute.For<Sets>();
        mockSets.Run().Throws(new DotenvxException("500", "Mock Error", "Mock Help"));
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        ParseResult parseResult = RootCommand.Parse(["set", "HELLO", "World"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockSets.Received(1).Run();
        MockLogger.ErrorMessages.ShouldContain("Mock Error. Mock Help");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: 500");
    }
}