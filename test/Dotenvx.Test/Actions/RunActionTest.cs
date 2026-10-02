using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

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
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run --convention"</remarks>
    [Fact]
    public void TestRunConventionNextjs()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "nextjs", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run --convention"</remarks>
    [Fact]
    public void TestRunConventionFlow()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "flow", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run - env"</remarks>
    [Fact]
    public void TestRunEnv()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (1) from --env flag");
    }

    /// <remarks>Refer to "run - envFile (with errors)"</remarks>
    [Fact]
    public void TestRunEnvFileWithErrors()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                        // Matches run.test.js's setCode() test helper: help/messageWithHelp are
                        // derived from Errors.ISSUE_BY_CODE the same way the real DotenvxException
                        // subclasses are, not a custom per-scenario string.
                        new DotenvxException("DECRYPTION_FAILED",
                            "[DECRYPTION_FAILED] could not decrypt HELLO using private key d607fff…",
                            "fix: [https://github.com/dotenvx/dotenvx/issues/757]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string> { { "HELLO", "encrypted:1234" } }
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.ErrorMessages.ShouldContain(
            "[DECRYPTION_FAILED] could not decrypt HELLO using private key d607fff…. fix: [https://github.com/dotenvx/dotenvx/issues/757]");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (1) from .env");
    }

    /// <remarks>Refer to "run - envFile"</remarks>
    [Fact]
    public void TestRunEnvFile()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (1) from .env");
    }

    /// <remarks>Refer to "run - envFile AND env"</remarks>
    [Fact]
    public void TestRunEnvFileAndEnv()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (1) from .env, and --env flag");
    }

    /// <remarks>Refer to "run - envFile AND two envs"</remarks>
    [Fact]
    public void TestRunEnvFileAndTwoEnvs()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse([
            "run", "-e", "HELLO=World", "-e", "HELLO=World", "-e", "HOLA=amigo", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HOLA=amigo)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.VerboseMessages.ShouldContain("HOLA set");
        MockLogger.DebugMessages.ShouldContain("HELLO set to World");
        MockLogger.DebugMessages.ShouldContain("HOLA set to amigo");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (2) from .env, and --env flags");
    }

    /// <remarks>Refer to "run - env (two strings)"</remarks>
    [Fact]
    public void TestRunEnvTwoStrings()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "-e", "HELLO=World", "-e", "HEY=there", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HELLO=World)");
        MockLogger.VerboseMessages.ShouldContain("loading env from string (HEY=there)");
        MockLogger.VerboseMessages.ShouldContain("HELLO set");
        MockLogger.VerboseMessages.ShouldContain("HEY set");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (2) from --env flags");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestRunMissingEnvFile()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                        // Matches run.test.js's exact "run - MISSING_ENV_FILE" test data.
                        new DotenvxException("MISSING_ENV_FILE",
                            "[MISSING_ENV_FILE] missing file (.env)",
                            "fix: [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] missing file (.env). fix: [echo \"HELLO=World\" > .env]");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>
    /// Refer to "run - MISSING_ENV_FILE fallback filepath". A `--env` string entry (Type == Env,
    /// no FilePath at all) still gets its error's own message printed as-is - the CLI's own
    /// per-error print never depends on processedEnv.FilePath, only on the error's own
    /// MessageWithHelp, so no crash/misformat happens when FilePath is absent.
    /// </remarks>
    [Fact]
    public void TestRunMissingEnvFileFallbackFilepath()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.Env,
                    String = "HELLO=World",
                    Parsed = new Dictionary<string, string>(),
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string>(),
                    Errors =
                    [
                        new DotenvxException("MISSING_ENV_FILE", "[MISSING_ENV_FILE] missing file (.env)",
                            "fix: [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] missing file (.env). fix: [echo \"HELLO=World\" > .env]");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE with --convention stays quiet"</remarks>
    [Fact]
    public void TestRunMissingEnvFileWithConventionStaysQuiet()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                        new DotenvxException("MISSING_ENV_FILE", "[MISSING_ENV_FILE] missing file (.env)",
                            "fix: [https://github.com/dotenvx/dotenvx/issues/484]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "nextjs", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.ErrorMessages.ShouldNotContain(s => s.Contains("MISSING_ENV_FILE"));
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE --strict flag"</remarks>
    [Fact]
    public void TestRunMissingEnvFileStrictFlag()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                        // Matches run.test.js's exact "run - MISSING_ENV_FILE --strict flag" test
                        // data. --strict throws, so this is logged via the outer catch's
                        // CatchAndLog rather than the per-error loop, but the combined message is
                        // identical either way.
                        new DotenvxException("MISSING_ENV_FILE",
                            "[MISSING_ENV_FILE] missing file (.env)",
                            "fix: [echo \"HELLO=World\" > .env]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--strict", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] missing file (.env). fix: [echo \"HELLO=World\" > .env]");
        MockLogger.InfoMessages.ShouldBeEmpty();
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE --ignore flag"</remarks>
    [Fact]
    public void TestRunMissingEnvFileIgnoreFlag()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse([
            "run", "--ignore", "MISSING_ENV_FILE", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain(
            "ignored: Mock Error");
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run - MISSING_ENV_FILE --strict flag and MISSING_ENV_FILE --ignore flag"</remarks>
    [Fact]
    public void TestRunMissingEnvFileStrictFlagIgnoreFlag()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse([
            "run", "--strict", "--ignore", "MISSING_ENV_FILE", "--", "echo", ""
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain(
            $"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain(
            "ignored: Mock Error");
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run - OTHER_ERROR"</remarks>
    [Fact]
    public void TestRunOtherError()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>
    /// Refer to "run - WRONG_PRIVATE_KEY". Uses the base DotenvxException with the exact literal
    /// upstream message (including the "…" truncation char already baked into the JS test's
    /// hardcoded string) rather than WrongPrivateKeyException's own Truncate() call, matching the
    /// established pattern in <see cref="TestRunEnvFileWithErrors"/> above - feeding a short,
    /// already-truncated-looking string through Truncate() would not reproduce the "…" the real
    /// upstream scenario has (a genuinely long key truncated down), and this test only cares about
    /// message pass-through, not truncation behavior itself.
    /// </remarks>
    [Fact]
    public void TestRunWrongPrivateKey()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                        new DotenvxException("WRONG_PRIVATE_KEY",
                            "[WRONG_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=199bdd6…'",
                            "fix: [https://github.com/dotenvx/dotenvx/issues/466]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.ErrorMessages.ShouldContain(
            "[WRONG_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=199bdd6…'. fix: [https://github.com/dotenvx/dotenvx/issues/466]");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>Refer to "run - MISSING_PRIVATE_KEY"</remarks>
    [Fact]
    public void TestRunMissingPrivateKey()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                    Errors = [new MissingPrivateKeyException("HELLO", "DOTENV_PRIVATE_KEY", "")]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY='. fix: [https://github.com/dotenvx/dotenvx/issues/464]");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0)");
    }

    /// <remarks>
    /// Refer to "run - preserves punctuated private key messages". Uses the base DotenvxException
    /// directly for the same reason as TestRunThrowsPunctuatedPrivateKeyErrors above.
    /// </remarks>
    [Fact]
    public void TestRunPreservesPunctuatedPrivateKeyMessages()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
                        new DotenvxException("WRONG_PRIVATE_KEY", "[WRONG_PRIVATE_KEY] punctuated",
                            "fix: [https://github.com/dotenvx/dotenvx/issues/466]"),
                        new DotenvxException("MISSING_PRIVATE_KEY", "[MISSING_PRIVATE_KEY] punctuated",
                            "fix: [https://github.com/dotenvx/dotenvx/issues/464]")
                    ]
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        parseResult.Invoke();

        MockLogger.ErrorMessages.ShouldContain(
            "[WRONG_PRIVATE_KEY] punctuated. fix: [https://github.com/dotenvx/dotenvx/issues/466]");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_PRIVATE_KEY] punctuated. fix: [https://github.com/dotenvx/dotenvx/issues/464]");
    }

    /// <remarks>Refer to "run - envFile (prexists)"</remarks>
    [Fact]
    public void TestRunEnvFilePrexists()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { { "HELLO", "World" } },
                    Injected = new Dictionary<string, string>(),
                    PreExisted = new Dictionary<string, string> { { "HELLO", "World" } }
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain("HELLO pre-exists (protip: use --overload to override)");
        MockLogger.DebugMessages.ShouldContain("HELLO pre-exists as World (protip: use --overload to override)");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0) from .env");
    }

    /// <remarks>
    /// This does NOT correspond to run.test.js's "run - throws error" (that's a generic
    /// Run.Execute() failure - see <see cref="TestRunThrowsError"/> below for that one; this
    /// method previously claimed the wrong title). This one is a CLI-integration check that an
    /// invalid --convention propagates correctly through `run` - the underlying INVALID_CONVENTION
    /// behavior itself is unit-tested directly in ConventionsTest.cs, matching
    /// dotenvx/tests/lib/helpers/conventions.test.js's "#conventions (invalid convention)".
    /// Mislabeling found via a Node-vs-C# unit test coverage audit, 2026-09-22.
    /// </remarks>
    [Fact]
    public void TestRunInvalidConvention()
    {
        // Mocking not needed here as we are testing CLI argument parsing failure which happens before Run.Execute
        ParseResult parseResult = RootCommand.Parse(["run", "--convention", "invalid", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[INVALID_CONVENTION] invalid convention (invalid). fix: [https://github.com/dotenvx/dotenvx/issues/761]");
    }

    /// <remarks>
    /// Refer to "run - throws error". A plain (non-DotenvxException) exception with no help text
    /// at all gets logged via just its own Message, through the outer catch's CatchAndLog.
    /// </remarks>
    [Fact]
    public void TestRunThrowsError()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Throws(new Exception("Mock Error"));
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
    }

    /// <remarks>
    /// Refer to "run - throws WRONG_PRIVATE_KEY". Uses the base DotenvxException with the exact
    /// literal upstream message for the same reason as <see cref="TestRunWrongPrivateKey"/> above.
    /// </remarks>
    [Fact]
    public void TestRunThrowsWrongPrivateKey()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Throws(new DotenvxException("WRONG_PRIVATE_KEY",
            "[WRONG_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=199bdd6…'",
            "fix: [https://github.com/dotenvx/dotenvx/issues/466]"));
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[WRONG_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=199bdd6…'. fix: [https://github.com/dotenvx/dotenvx/issues/466]");
    }

    /// <remarks>Refer to "run - throws MISSING_PRIVATE_KEY"</remarks>
    [Fact]
    public void TestRunThrowsMissingPrivateKey()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Throws(new MissingPrivateKeyException("HELLO", "DOTENV_PRIVATE_KEY", ""));
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY='. fix: [https://github.com/dotenvx/dotenvx/issues/464]");
    }

    /// <remarks>
    /// Refer to "run - throws punctuated private key errors". Uses the base DotenvxException
    /// directly (not the Wrong/MissingPrivateKeyException subclasses, which always build their own
    /// fixed message shape) since the JS test constructs a generic error with an arbitrary message
    /// ("punctuated") and only relies on the error CODE to derive the correct help/fix URL.
    /// </remarks>
    [Fact]
    public void TestRunThrowsPunctuatedPrivateKeyErrors()
    {
        Run mockWrongRun = Substitute.For<Run>();
        mockWrongRun.Execute().Throws(new DotenvxException("WRONG_PRIVATE_KEY", "[WRONG_PRIVATE_KEY] punctuated",
            "fix: [https://github.com/dotenvx/dotenvx/issues/466]"));
        Run.Factory = (_, _, _, _) => mockWrongRun;

        RootCommand.Parse(["run", "--", "echo", ""]).Invoke();
        MockLogger.ErrorMessages.ShouldContain(
            "[WRONG_PRIVATE_KEY] punctuated. fix: [https://github.com/dotenvx/dotenvx/issues/466]");

        Run mockMissingRun = Substitute.For<Run>();
        mockMissingRun.Execute().Throws(new DotenvxException("MISSING_PRIVATE_KEY",
            "[MISSING_PRIVATE_KEY] punctuated", "fix: [https://github.com/dotenvx/dotenvx/issues/464]"));
        Run.Factory = (_, _, _, _) => mockMissingRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_PRIVATE_KEY] punctuated. fix: [https://github.com/dotenvx/dotenvx/issues/464]");
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

    /// <remarks>
    /// Refer to "run - envFile (ambiguous arguments, missing --)". This test previously asserted
    /// the OPPOSITE of what the real upstream test does - the actual JS test (dotenvx/tests/cli/
    /// actions/run.test.js) stubs `args: []` and `process.argv` to this exact shape and asserts
    /// SUCCESS (envsResolver called, verbose/debug/success logged normally) via `run.js`'s
    /// `inferCommandArgsFromProcessArgv` fallback - it does NOT assert the "ambiguous command"
    /// error. That error path is real (see the two dedicated tests below for it), but this specific
    /// scenario - `-f &lt;file&gt; command args` with no `--` at all - is exactly the case the
    /// fallback exists to handle, matching how real `dotenvx run -f .env node -e "..."` (no `--`)
    /// behaves live. Ported wrong originally (misread from the test's own name alone, without
    /// checking what it actually asserts) - fixed 2026-09-29 alongside `RunAction.cs`'s gate.
    /// </remarks>
    [Fact]
    public void TestRunEnvFileAmbiguousArgumentsMissingDoubleDash()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { ["HELLO"] = "World" },
                    Injected = new Dictionary<string, string> { ["HELLO"] = "World" },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "-f", ".env.production", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (1) from .env");
    }

    /// <remarks>
    /// Refer to "run - envFile (ambiguous arguments, missing -- and envFile is empty)" - same fix
    /// as the test above, see its remarks for why.
    /// </remarks>
    [Fact]
    public void TestRunEnvFileAmbiguousArgumentsMissingDoubleDashEnvFileEmpty()
    {
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs =
            [
                new RunProcessedEnv
                {
                    Type = EnvType.EnvFile,
                    FilePath = ".env",
                    Parsed = new Dictionary<string, string> { ["HELLO"] = "World" },
                    Injected = new Dictionary<string, string> { ["HELLO"] = "World" },
                    PreExisted = new Dictionary<string, string>()
                }
            ],
            ReadableStrings = [],
            ReadableFilePaths = [".env"],
            UniqueInjectedKeys = ["HELLO"],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "-f", ".env", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockRun.Received(1).Execute();
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (1) from .env");
    }

    /// <remarks>
    ///     Refer to "run - envFile - parsed, injected, and preExisted missing for some reason upstream - it doesn\'t
    ///     choke"
    /// </remarks>
    [Fact]
    public void TestRunEnvFileMissingParsedInjectedPreExistedDoesNotChoke()
    {
        // Mock Run to return incomplete processedEnvs data
        Run mockRun = Substitute.For<Run>();
        mockRun.Execute().Returns(new RunResult
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
            (_, _, _, _) => mockRun;

        ParseResult parseResult = RootCommand.Parse(["run", "--", "echo", ""]);

        parseResult.Invoke();

        MockLogger.VerboseMessages.ShouldContain($"loading env from .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (0) from .env");
    }

}