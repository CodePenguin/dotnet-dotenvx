using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Moq;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/run.test.js</remarks>
public class RunActionTest : EnvironmentTestBase
{
    public RunActionTest()
    {
        ClearEnvironmentExceptPath();
        RootCommand = new LoggingRootCommand { new RunAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }


    /// <remarks>Refer to "run"</remarks>
    [Fact]
    public void TestRun()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run --convention"</remarks>
    [Fact]
    public void TestRunConventionNextjs()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "nextjs", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run --convention"</remarks>
    [Fact]
    public void TestRunConventionFlow()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "flow", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run - env"</remarks>
    [Fact]
    public void TestRunEnv()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HELLO=World",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = ["HELLO=World"],
            ReadableFilePaths = [],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "World" } }
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from --env flag");
    }

    /// <remarks>Refer to "run - envFile (with errors)"</remarks>
    [Fact]
    public void TestRunEnvFileWithErrors()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    Errors =
                    [
                        new DotenvxException("DECRYPTION_FAILED",
                            "[DECRYPTION_FAILED] could not decrypt HELLO using private key d607fff…",
                            "[DECRYPTION_FAILED] ? encrypted data looks malformed")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "encrypted:1234" } }
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.ErrorMessages.ShouldContain(
            "[DECRYPTION_FAILED] could not decrypt HELLO using private key d607fff…");
        MockLogger.ErrorMessages.ShouldContain("[DECRYPTION_FAILED] ? encrypted data looks malformed");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from .env");
    }

    /// <remarks>Refer to "run - envFile"</remarks>
    [Fact]
    public void TestRunEnvFile()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "World" } }
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from .env");
    }

    /// <remarks>Refer to "run - envFile AND env"</remarks>
    [Fact]
    public void TestRunEnvFileAndEnv()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                },
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HELLO=World",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = ["HELLO=World"],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "World" } }
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("injecting env (1) from .env, and --env flag");
    }

    /// <remarks>Refer to "run - envFile AND two envs"</remarks>
    [Fact]
    public void TestRunEnvFileAndTwoEnvs()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                },
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HELLO=World",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                },
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HOLA=amigo",
                    Parsed = new Dictionary<string, string> { { "HOLA", "amigo" } },
                    Injected = new Dictionary<string, string> { { "HOLA", "amigo" } },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = ["HELLO=World", "HOLA=amigo"],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO", "HOLA"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "World" }, { "HOLA", "amigo" } }
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse([
            "run", "-e", "HELLO=World", "-e", "HELLO=World", "-e", "HOLA=amigo", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HOLA=amigo)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.VerboseMessages.ShouldContain("HOLA set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.DebugMessages.ShouldContain("HOLA set to amigo");
        MockLogger.InfoMessages.ShouldContain("injecting env (2) from .env, and --env flags");
    }

    /// <remarks>Refer to "run - env (two strings)"</remarks>
    [Fact]
    public void TestRunEnvTwoStrings()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HELLO=World",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string> { { "HELLO", "World" } },
                    PreExisted = new Dictionary<string, string>()
                },
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HEY=there",
                    Parsed = new Dictionary<string, string> { { "HEY", "there" } },
                    Injected = new Dictionary<string, string> { { "HEY", "there" } },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = ["HELLO=World", "HEY=there"],
            ReadableFilePaths = [],
            UniqueInjectedKeys = ["HELLO", "HEY"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "World" }, { "HEY", "there" } }
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "-e", "HEY=there", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HEY=there)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.VerboseMessages.ShouldContain("HEY set");
        MockLogger.InfoMessages.ShouldContain("injecting env (2) from --env flags");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestRunMissingEnvFile()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>(),
                    Errors =
                    [
                        new DotenvxException("MISSING_ENV_FILE",
                            "Mock Error",
                            "[MISSING_ENV_FILE] ? add one with [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] ? add one with [echo \"HELLO=World\" > .env] and re-run [dotenvx run -- echo ]");
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE --strict flag"</remarks>
    [Fact]
    public void TestRunMissingEnvFileStrictFlag()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>(),
                    Errors =
                    [
                        new DotenvxException("MISSING_ENV_FILE",
                            "Mock Error",
                            "[MISSING_ENV_FILE] ? add one with [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--strict", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.ErrorMessages.ShouldContain("[MISSING_ENV_FILE] ? add one with [echo \"HELLO=World\" > .env]");
        MockLogger.InfoMessages.ShouldBeEmpty();
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE --ignore flag"</remarks>
    [Fact]
    public void TestRunMissingEnvFileIgnoreFlag()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>(),
                    Errors =
                    [
                        new DotenvxException("MISSING_ENV_FILE", "Mock Error",
                            "[MISSING_ENV_FILE] ? add one with [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse([
            "run", "--ignore", "MISSING_ENV_FILE", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain(
            "ignored: Mock Error");
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE --strict flag and MISSING_ENV_FILE --ignore flag"</remarks>
    [Fact]
    public void TestRunMissingEnvFileStrictFlagIgnoreFlag()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>(),
                    Errors =
                    [
                        new DotenvxException("MISSING_ENV_FILE",
                            "Mock Error",
                            "[MISSING_ENV_FILE] ? add one with [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse([
            "run", "--strict", "--ignore", "MISSING_ENV_FILE", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain(
            "ignored: Mock Error");
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run - OTHER_ERROR"</remarks>
    [Fact]
    public void TestRunOtherError()
    {
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>(),
                    Errors =
                    [
                        new DotenvxException("OTHER_ERROR",
                            "Mock Error",
                            "")
                    ]
                },
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "INVALID",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.InfoMessages.ShouldContain("injecting env (0)");
    }

    /// <remarks>Refer to "run - throws error"</remarks>
    [Fact]
    public void TestRunThrowsError()
    {
        // Mocking not needed here as we are testing CLI argument parsing failure which happens before Run.Execute
        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "invalid", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "INVALID_CONVENTION: 'invalid'. permitted conventions: ['nextjs', 'flow']");
    }

    /// <remarks>Refer to "run - envFile (missing command arguments after --)"</remarks>
    [Fact]
    public void TestRunEnvFileMissingCommandArgumentsAfterDoubleDash()
    {
        // Mocking not needed as this is CLI parsing error
        ParseResult parseResult = RootCommand.Parse(["run", "--"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "missing command after [dotenvx run --]. try [dotenvx run -- yourcommand]");
    }

    /// <remarks>Refer to "run - envFile (ambiguous arguments, missing --)"</remarks>
    [Fact]
    public void TestRunEnvFileAmbiguousArgumentsMissingDoubleDash()
    {
        // Mocking not needed as this is CLI parsing error
        ParseResult parseResult = RootCommand.Parse(["run", "-f", ".env.production", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "ambiguous command due to missing \'--\' separator. try [dotenvx run -f .env.production -- yourcommand]");
    }

    /// <remarks>Refer to "run - envFile (ambiguous arguments, missing -- and envFile is empty)"</remarks>
    [Fact]
    public void TestRunEnvFileAmbiguousArgumentsMissingDoubleDashEnvFileEmpty()
    {
        // Mocking not needed as this is CLI parsing error
        ParseResult parseResult = RootCommand.Parse(["run", "-f", ".env", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "ambiguous command due to missing \'--\' separator. try [dotenvx run -f .env -- yourcommand]");
    }

    /// <remarks>
    ///     Refer to "run - envFile - parsed, injected, and preExisted missing for some reason upstream - it doesn\'t
    ///     choke"
    /// </remarks>
    [Fact]
    public void TestRunEnvFileMissingParsedInjectedPreExistedDoesNotChoke()
    {
        // Mock Run to return incomplete processedEnvs data
        Mock<Run> mockRun = new();
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile, FilePath = ".env"
                    // Missing Parsed, Injected, PreExisted, Errors
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory =
            (_, _, _, _) => mockRun.Object;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);

        parseResult.Invoke();

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("injecting env (0) from .env");
    }

}