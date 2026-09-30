using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs;

/// <remarks>Reference source: dotenvx/tests/lib/main.test.js</remarks>
public class MainTest : EnvironmentTestBase
{
    private const string BackendEnvFile = "tests/monorepo/apps/backend/.env";
    private const string FrontendEnvFile = "tests/monorepo/apps/frontend/.env";
    private const string FrontendPath = "tests/monorepo/apps/frontend";

    public MainTest()
    {
        ClearEnvironment();
        Fsx.WriteFileX = (_, _) => { };
    }

    /// <remarks>Refer to "config calls Run.run"</remarks>
    [Fact]
    public void TestConfigCallsRunRun()
    {
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Received(1).Execute();
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config with convention - calls Run.run with proper envs"</remarks>
    [Fact]
    public void TestConfigWithConventionCallsRunRunWithProperEnvs()
    {
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        ConfigResult result = Main.Config(new ConfigOptions { Convention = "nextjs" });

        mockRun.Received(1).Execute();
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config with convention flow - calls Run.run with proper envs"</remarks>
    [Fact]
    public void TestConfigWithConventionFlowCallsRunRunWithProperEnvs()
    {
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        ConfigResult result = Main.Config(new ConfigOptions { Convention = "flow" });

        mockRun.Received(1).Execute();
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config with Run.run errors"</remarks>
    [Fact]
    public void TestConfigWithRunRunErrors()
    {
        DotenvxException error = new("SOME_ERROR", "some error", "some help");
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs =
                [new RunProcessedEnv { Errors = [error] }],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ConfigResult result = Main.Config(new ConfigOptions { Logger = MockLogger });

        mockRun.Received(1).Execute();
        MockLogger.ErrorMessages.ShouldContain("some error. some help");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "config with Run.run errors and ignore"</remarks>
    [Fact]
    public void TestConfigWithRunRunErrorsAndIgnore()
    {
        DotenvxException error = new("SOME_ERROR", "some error", "some help");
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs =
                [new RunProcessedEnv { Errors = [error] }],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ConfigResult result = Main.Config(new ConfigOptions { Ignore = ["SOME_ERROR"] });

        mockRun.Received(1).Execute();
        MockLogger.ErrorMessages.ShouldBeEmpty();
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "parse calls Parse.run"</remarks>
    [Fact]
    public void TestParseCallsParseRun()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse("HELLO=World");

        parsed["HELLO"].ShouldBe("World");
    }

    /// <remarks>Refer to "parse calls Parse.run with options.processEnv"</remarks>
    [Fact]
    public void TestParseCallsParseRunWithOptionsProcessEnv()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse("HELLO=World",
            new ParseOptions { ProcessEnv = new Dictionary<string, string>() });

        parsed["HELLO"].ShouldBe("World");
    }

    /// <remarks>Refer to "parse calls Parse.run with options.privateKey"</remarks>
    [Fact]
    public void TestParseCallsParseRunWithOptionsPrivateKey()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(
            "HELLO=\"encrypted:BE9Y7LKANx77X1pv1HnEoil93fPa5c9rpL/1ps48uaRT9zM8VR6mHx9yM+HktKdsPGIZELuZ7rr2mn1gScsmWitppAgE/1lVprNYBCqiYeaTcKXjDUXU5LfsEsflnAsDhT/kWG1l\"",
            new ParseOptions { PrivateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c" });

        parsed["HELLO"].ShouldBe("World");
    }

    /// <remarks>Refer to "parse calls Parse.run with invalid options.privateKey"</remarks>
    [Fact]
    public void TestParseCallsParseRunWithInvalidOptionsPrivateKey()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(
            "HELLO=\"encrypted:BE9Y7LKANx77X1pv1HnEoil93fPa5c9rpL/1ps48uaRT9zM8VR6mHx9yM+HktKdsPGIZELuZ7rr2mn1gScsmWitppAgE/1lVprNYBCqiYeaTcKXjDUXU5LfsEsflnAsDhT/kWG1l\"",
            new ParseOptions { PrivateKey = "12345", Logger = MockLogger });

        parsed["HELLO"]
            .ShouldBe(
                "encrypted:BE9Y7LKANx77X1pv1HnEoil93fPa5c9rpL/1ps48uaRT9zM8VR6mHx9yM+HktKdsPGIZELuZ7rr2mn1gScsmWitppAgE/1lVprNYBCqiYeaTcKXjDUXU5LfsEsflnAsDhT/kWG1l");
        MockLogger.ErrorMessages.ShouldNotBeEmpty();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnv()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env already set"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAlreadySet()
    {
        Dictionary<string, string> processEnv = new() { ["HELLO"] = "world" };
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("world");
        result.Parsed["HELLO"].ShouldBe("world");
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env already set --overload"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAlreadySetOverload()
    {
        Dictionary<string, string> processEnv = new() { ["HELLO"] = "world" };
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile], Overload = true };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND frontend/.env"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndFrontendEnv()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendEnvFile] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND frontend/.env --overload"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndFrontendEnvOverload()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendEnvFile], Overload = true
        };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("frontend");
        result.Parsed["HELLO"].ShouldBe("frontend");
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND frontend/missing"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndFrontendMissing()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv, Path = [BackendEnvFile, "tests/monorepo/apps/frontend/missing"]
        };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("MISSING_ENV_FILE");
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND attempt on directory frontend"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontend()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendPath] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    /// <remarks>
    /// Refer to "config monorepo/apps/backend/.env AND directory frontend --strict" - renamed
    /// (dropping "attempt on") and rewritten upstream once `buildConfigEnvs`/directory resolution
    /// landed: a directory Path entry now correctly resolves to `{directory}/.env` instead of being
    /// treated as a literal, nonexistent filename. Confirmed via `dotenvx/tests/lib/main.test.js` -
    /// there's a SEPARATE, still-current "get monorepo/apps/backend/.env AND attempt on directory
    /// frontend --strict it throws" test with the OLD title/behavior, because get() (unlike
    /// config()) never routes through buildCommandEnvs at all (see BuildConfigEnvs.cs's doc comment
    /// and CLAUDE.md's Main.cs audit trap) - this port's OWN two tests here had been misported
    /// against config()'s now-superseded pre-buildConfigEnvs behavior.
    /// </remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndDirectoryFrontendStrict()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendPath], Strict = true };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND attempt on directory frontend --strict but error ALSO ignored"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictButErrorAlsoIgnored()
    {
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv,
            Path = [BackendEnvFile, FrontendPath],
            Strict = true,
            Ignore = ["MISSING_ENV_FILE"]
        };

        ConfigResult result = Main.Config(options);

        mockRun.Received(1).Execute();
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "config with Run.run processedEnv with undefined processedEnv.errors"</remarks>
    [Fact]
    public void TestConfigWithRunRunProcessedEnvWithUndefinedProcessedEnvErrors()
    {
        Run mockRun = Substitute.For<Run>(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Execute().Returns(new RunResult
        {
            ProcessedEnvs = [new RunProcessedEnv()],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun;

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Received(1).Execute();
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "ls calls Ls.run"</remarks>
    [Fact]
    public void TestLsCallsLsRun()
    {
        Ls mockLs = Substitute.For<Ls>("./", null!, null!);
        mockLs.Run().Returns([]);
        Ls.Factory = (_, _, _) => mockLs;

        string[] result = Main.Ls();

        mockLs.Received(1).Run();
        result.ShouldBeOfType<string[]>();
    }

    /// <remarks>Refer to "keypair calls Keypair.run"</remarks>
    [Fact]
    public void TestKeyPairCallsKeyPairRun()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>(".env", null!);
        mockKeyPair.Run().Returns(new Dictionary<string, string?>());
        KeyPair.Factory = (_, _) => mockKeyPair;

        IReadOnlyDictionary<string, string> result = Main.KeyPair();

        mockKeyPair.Received(1).Run();
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "keypair calls Keypair.run with key specified"</remarks>
    [Fact]
    public void TestKeyPairCallsKeyPairRunWithKeySpecified()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>(".env", null!);
        mockKeyPair.Run().Returns(new Dictionary<string, string?> { ["KEY"] = "value" });
        KeyPair.Factory = (_, _) => mockKeyPair;

        IReadOnlyDictionary<string, string> result = Main.KeyPair(".env", "KEY");

        mockKeyPair.Received(1).Run();
        result.ShouldBeOfType<Dictionary<string, string>>();
        result["KEY"].ShouldBe("value");
    }

    /// <remarks>Refer to "set calls Sets.run"</remarks>
    [Fact]
    public void TestSetCallsSetsRun()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        mockSets.Run()
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsResult result = Main.Set("KEY", "value");

        mockSets.Received(1).Run();
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run with encrypt false"</remarks>
    [Fact]
    public void TestSetCallsSetsRunWithEncryptFalse()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        mockSets.Run()
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, encrypt, _, _) =>
        {
            encrypt.ShouldBeFalse();
            return mockSets;
        };

        SetsOptions options = new() { Encrypt = false };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Received(1).Run();
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run with plain true"</remarks>
    [Fact]
    public void TestSetCallsSetsRunWithPlainTrue()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        mockSets.Run()
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, encrypt, _, _) =>
        {
            encrypt.ShouldBeFalse();
            return mockSets;
        };

        SetsOptions options = new() { Plain = true };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Received(1).Run();
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run with custom envKeysFile"</remarks>
    [Fact]
    public void TestSetCallsSetsRunWithCustomEnvKeysFile()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        mockSets.Run()
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, _, envKeysFile, _) =>
        {
            envKeysFile.ShouldBe("path/to/.env.keys");
            return mockSets;
        };

        SetsOptions options = new() { EnvKeysFile = "path/to/.env.keys" };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Received(1).Run();
        result.ShouldNotBeNull();
    }

    [Fact]
    public void TestSetCallsSetsRunWithNoCreateFromOptions()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        mockSets.Run()
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, _, _, noCreate) =>
        {
            noCreate.ShouldBeTrue();
            return mockSets;
        };

        SetsOptions options = new() { NoCreate = true };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Received(1).Run();
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - no "set calls Sets.run..." test exists in
    /// the current dotenvx/tests/lib/main.test.js at all. Assertion corrected 2026-09-29 (was
    /// locking in Main.Set()'s wrong "no changes" text/symbol; real main.js's set() prints
    /// "○ no change (...)", matching every other command's no-change message).
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunNoChanges()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = false,
                Error = null
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths = [],
            UnchangedFilePaths =
                [".env"]
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("○ no change (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - see TestSetCallsSetsRunNoChanges' remark.
    /// Assertion corrected 2026-09-29: real main.js's set() prints "◈ encrypted {key} (...)" when
    /// encrypting, not the fabricated "✔ set {key} with encryption (...)".
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunChanges()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = true,
                Error = null
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths =
            [
                ".env"
            ],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("◈ encrypted HELLO (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - see TestSetCallsSetsRunNoChanges' remark.
    /// Assertion corrected 2026-09-29: real main.js's set() prints exactly ONE combined
    /// `logger.warn(message)` per errored file (message = error.messageWithHelp when available),
    /// not a separate Warn(message) + Help(customText) pair - the "? add one with [echo ...]"
    /// text has no upstream basis at all (same fabricated-help-text bug class as CLAUDE.md's
    /// original error-message-printing trap).
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunMissingEnvFile()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        Exception error = new("MISSING_ENV_FILE");
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = false,
                Error = error
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs, ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.WarningMessages.ShouldContain("MISSING_ENV_FILE");
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - see TestSetCallsSetsRunNoChanges' remark.
    /// Assertion corrected 2026-09-29 to the same single-combined-Warn-call fix described on
    /// TestSetCallsSetsRunMissingEnvFile.
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunOtherError()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        DotenvxException error = new("OTHER_ERROR", "some error", "some help");
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = false,
                Error = error
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs, ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.WarningMessages.ShouldContain("some error. some help");
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - see TestSetCallsSetsRunNoChanges' remark.
    /// Duplicate of TestSetCallsSetsRunChanges (same scenario, different name) - kept as its own
    /// test since it predates this file's traceability convention; assertion corrected the same way.
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunChangesWithLogger()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = true,
                Error = null
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths =
            [
                ".env"
            ],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("◈ encrypted HELLO (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - see TestSetCallsSetsRunNoChanges' remark.
    /// Assertion corrected 2026-09-29: when a changed file exists, real main.js's set() prints only
    /// the ONE "◈ encrypted {key} (...)" success line (`if (changedFilepaths.length > 0) {...} else
    /// if (encrypt &amp;&amp; localKeyAddedEnv) {...}` - the two branches are mutually exclusive) - the
    /// separate "✔ key added to .env.keys (...)" success line plus its two "next run [...]" help
    /// lines have no upstream basis at all (same fabricated-message bug class already found in
    /// RotateAction.cs - see CLAUDE.md trap #27). This port also has no native/armor key storage,
    /// so the real `localKeyAddedEnv`/" · armored ⛨" branch this test's NAME references is only
    /// reachable here via <see cref="TestSetCallsSetsRunLocalKeyAddedWithNoChangedFiles"/> below,
    /// which actually exercises it (unchanged files, key still freshly added).
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunPrivateKeyAddedAndNotIgnoringDotenvKeys()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = true,
                Error = null,
                PrivateKeyAdded = true,
                EnvKeysFilePath = ".env.keys",
                PrivateKeyName = "DOTENV_PRIVATE_KEY",
                PrivateKey = "1234"
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths =
            [
                ".env"
            ],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("◈ encrypted HELLO (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>
    /// Not from a JS test - dedicated coverage for main.js's `else if (encrypt &amp;&amp;
    /// localKeyAddedEnv)` branch (the ONLY currently-reachable path since this port has no
    /// remote/armor key storage): a fresh keypair was generated (PrivateKeyAdded) but the VALUE
    /// itself didn't change (empty ChangedFilePaths, matching Sets.cs's real "identical value" no-op
    /// - see CLAUDE.md's Sets.cs re-encryption trap), so the success line still needs to report the
    /// file the key was added to, using localKeyAddedEnv.EnvFilePath as the fallback source.
    /// </remarks>
    [Fact]
    public void TestSetCallsSetsRunLocalKeyAddedWithNoChangedFiles()
    {
        Sets mockSets = Substitute.For<Sets>("KEY", "value", Array.Empty<EnvItem>(), true, null!, false);
        ProcessedSet[] processedEnvs =
        [
            new()
            {
                Key = "HELLO",
                Value = "World",
                FilePath = ".env",
                EnvFilePath = ".env",
                EnvSrc = "HELLO=World",
                Changed = false,
                Error = null,
                PrivateKeyAdded = true,
                EnvKeysFilePath = ".env.keys",
                PrivateKeyName = "DOTENV_PRIVATE_KEY",
                PrivateKey = "1234"
            }
        ];
        mockSets.Run().Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs, ChangedFilePaths = [], UnchangedFilePaths = [".env"]
        });
        Sets.Factory = (_, _, _, _, _, _) => mockSets;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("◈ encrypted HELLO (.env)");
        MockLogger.InfoMessages.ShouldNotContain("○ no change (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "get calls Get.run"</remarks>
    [Fact]
    public void TestGetCallsGetRun()
    {
        Get mockGet = Substitute.For<Get>("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run()
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet;

        object? result = Main.Get("KEY");

        mockGet.Received(1).Run();
        result.ShouldBe("value");
    }

    /// <remarks>Refer to "get calls Get.run undefined"</remarks>
    [Fact]
    public void TestGetCallsGetRunUndefined()
    {
        Get mockGet = Substitute.For<Get>("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run().Returns(new GetResult { Parsed = new Dictionary<string, string>(), Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet;

        object? result = Main.Get("KEY");

        mockGet.Received(1).Run();
        result.ShouldBeNull();
    }

    /// <remarks>Refer to "get calls Get.run with no key"</remarks>
    [Fact]
    public void TestGetCallsGetRunWithNoKey()
    {
        Get mockGet = Substitute.For<Get>(null!, Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run()
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet;

        object? result = Main.Get(null);

        mockGet.Received(1).Run();
        ((IReadOnlyDictionary<string, string>)result!)["KEY"].ShouldBe("value");
    }

    /// <remarks>Refer to "get calls Get.run format eval"</remarks>
    [Fact]
    public void TestGetCallsGetRunFormatEval()
    {
        Get mockGet = Substitute.For<Get>(null!, Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run()
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet;

        GetOptions options = new() { Format = "eval" };

        object? result = Main.Get(null, options);

        mockGet.Received(1).Run();
        result.ShouldBe("KEY='value'");
    }

    /// <remarks>Refer to "get calls Get.run format shell"</remarks>
    [Fact]
    public void TestGetCallsGetRunFormatShell()
    {
        Get mockGet = Substitute.For<Get>(null!, Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run()
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet;

        GetOptions options = new() { Format = "shell" };

        object? result = Main.Get(null, options);

        mockGet.Received(1).Run();
        result.ShouldBe("KEY=value");
    }

    /// <remarks>Refer to "get monorepo/apps/backend/.env AND attempt on directory frontend --strict it throws"</remarks>
    [Fact]
    public void TestGetMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictItThrows()
    {
        Dictionary<string, string> processEnv = new();

        GetOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendPath], Strict = true };

        DotenvxException exception = Should.Throw<DotenvxException>(() => Main.Get("HELLO", options));

        exception.Message.ShouldContain($"[MISSING_ENV_FILE] missing file ({FrontendPath})");
    }

    /// <remarks>Refer to "get with Get.run errors"</remarks>
    [Fact]
    public void TestGetWithGetRunErrors()
    {
        Get mockGet = Substitute.For<Get>("KEY", Array.Empty<EnvItem>(), false, false, null!);
        Exception error = new("some error");
        mockGet.Run().Returns(new GetResult
        {
            Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [error]
        });
        Get.Factory = (_, _, _, _, _) => mockGet;

        GetOptions options = new() { Logger = MockLogger };

        _ = Main.Get("KEY", options);

        mockGet.Received(1).Run();
        MockLogger.ErrorMessages.ShouldContain("some error");
    }

    /// <remarks>Refer to "get with Get.run undefined errors"</remarks>
    [Fact]
    public void TestGetWithGetRunUndefinedErrors()
    {
        Get mockGet = Substitute.For<Get>("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run()
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet;

        GetOptions options = new() { Logger = MockLogger };

        _ = Main.Get("KEY", options);

        mockGet.Received(1).Run();
        MockLogger.ErrorMessages.ShouldBeEmpty();
    }

    /// <remarks>Refer to "get with Get.run errors and ignore"</remarks>
    [Fact]
    public void TestGetWithGetRunErrorsAndIgnore()
    {
        Get mockGet = Substitute.For<Get>("KEY", Array.Empty<EnvItem>(), false, false, null!);
        DotenvxException error = new("SOME_ERROR", "some error");
        mockGet.Run().Returns(new GetResult
        {
            Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [error]
        });
        Get.Factory = (_, _, _, _, _) => mockGet;

        GetOptions options = new() { Logger = MockLogger, Ignore = ["SOME_ERROR"] };

        _ = Main.Get("KEY", options);

        mockGet.Received(1).Run();
        MockLogger.ErrorMessages.ShouldBeEmpty();
    }
}