using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Moq;

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
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config with convention - calls Run.run with proper envs"</remarks>
    [Fact]
    public void TestConfigWithConventionCallsRunRunWithProperEnvs()
    {
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        ConfigResult result = Main.Config(new ConfigOptions { Convention = "nextjs" });

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config with convention flow - calls Run.run with proper envs"</remarks>
    [Fact]
    public void TestConfigWithConventionFlowCallsRunRunWithProperEnvs()
    {
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        ConfigResult result = Main.Config(new ConfigOptions { Convention = "flow" });

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "config with Run.run errors"</remarks>
    [Fact]
    public void TestConfigWithRunRunErrors()
    {
        DotenvxException error = new("SOME_ERROR", "some error", "some help");
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
                [new RunProcessedEnv { Errors = [error] }],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions { Logger = MockLogger });

        mockRun.Verify(r => r.Execute(), Times.Once);
        MockLogger.ErrorMessages.ShouldContain("some error");
        MockLogger.ErrorMessages.ShouldContain("some help");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "config with Run.run errors and ignore"</remarks>
    [Fact]
    public void TestConfigWithRunRunErrorsAndIgnore()
    {
        DotenvxException error = new("SOME_ERROR", "some error", "some help");
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
                [new RunProcessedEnv { Errors = [error] }],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions { Ignore = ["SOME_ERROR"] });

        mockRun.Verify(r => r.Execute(), Times.Once);
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
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("MISSING_ENV_FILE");
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND attempt on directory frontend --strict it throws"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictItThrows()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendPath], Strict = true };

        DotenvxException exception = Should.Throw<DotenvxException>(() => Main.Config(options));
        exception.Message.ShouldContain("MISSING_ENV_FILE");
    }

    /// <remarks>Refer to "config monorepo/apps/backend/.env AND attempt on directory frontend --strict but error ALSO ignored"</remarks>
    [Fact]
    public void TestConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictButErrorAlsoIgnored()
    {
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
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

        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv,
            Path = [BackendEnvFile, FrontendPath],
            Strict = true,
            Ignore = ["MISSING_ENV_FILE"]
        };

        ConfigResult result = Main.Config(options);

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "config with Run.run processedEnv with undefined processedEnv.errors"</remarks>
    [Fact]
    public void TestConfigWithRunRunProcessedEnvWithUndefinedProcessedEnvErrors()
    {
        Mock<Run> mockRun = new(Array.Empty<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [new RunProcessedEnv()],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Run.Factory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    /// <remarks>Refer to "ls calls Ls.run"</remarks>
    [Fact]
    public void TestLsCallsLsRun()
    {
        Mock<Ls> mockLs = new("./", null!, null!);
        mockLs.Setup(l => l.Run()).Returns([]);
        Ls.Factory = (_, _, _) => mockLs.Object;

        string[] result = Main.Ls();

        mockLs.Verify(l => l.Run(), Times.Once);
        result.ShouldBeOfType<string[]>();
    }

    /// <remarks>Refer to "keypair calls Keypair.run"</remarks>
    [Fact]
    public void TestKeyPairCallsKeyPairRun()
    {
        Mock<KeyPair> mockKeyPair = new(".env", null!);
        mockKeyPair.Setup(k => k.Run()).Returns(new Dictionary<string, string?>());
        KeyPair.Factory = (_, _) => mockKeyPair.Object;

        IReadOnlyDictionary<string, string> result = Main.KeyPair();

        mockKeyPair.Verify(k => k.Run(), Times.Once);
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "keypair calls Keypair.run with key specified"</remarks>
    [Fact]
    public void TestKeyPairCallsKeyPairRunWithKeySpecified()
    {
        Mock<KeyPair> mockKeyPair = new(".env", null!);
        mockKeyPair.Setup(k => k.Run()).Returns(new Dictionary<string, string?> { ["KEY"] = "value" });
        KeyPair.Factory = (_, _) => mockKeyPair.Object;

        IReadOnlyDictionary<string, string> result = Main.KeyPair(".env", "KEY");

        mockKeyPair.Verify(k => k.Run(), Times.Once);
        result.ShouldBeOfType<Dictionary<string, string>>();
        result["KEY"].ShouldBe("value");
    }

    /// <remarks>Refer to "set calls Sets.run"</remarks>
    [Fact]
    public void TestSetCallsSetsRun()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
        mockSets.Setup(s => s.Run())
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsResult result = Main.Set("KEY", "value");

        mockSets.Verify(s => s.Run(), Times.Once);
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run with encrypt false"</remarks>
    [Fact]
    public void TestSetCallsSetsRunWithEncryptFalse()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
        mockSets.Setup(s => s.Run())
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, encrypt, _) =>
        {
            encrypt.ShouldBeFalse();
            return mockSets.Object;
        };

        SetsOptions options = new() { Encrypt = false };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run with plain true"</remarks>
    [Fact]
    public void TestSetCallsSetsRunWithPlainTrue()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
        mockSets.Setup(s => s.Run())
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, encrypt, _) =>
        {
            encrypt.ShouldBeFalse();
            return mockSets.Object;
        };

        SetsOptions options = new() { Plain = true };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run with custom envKeysFile"</remarks>
    [Fact]
    public void TestSetCallsSetsRunWithCustomEnvKeysFile()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
        mockSets.Setup(s => s.Run())
            .Returns(new SetsResult { ProcessedEnvs = [], ChangedFilePaths = [], UnchangedFilePaths = [] });
        Sets.Factory = (_, _, _, _, envKeysFile) =>
        {
            envKeysFile.ShouldBe("path/to/.env.keys");
            return mockSets.Object;
        };

        SetsOptions options = new() { EnvKeysFile = "path/to/.env.keys" };

        SetsResult result = Main.Set("KEY", "value", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run - no changes"</remarks>
    [Fact]
    public void TestSetCallsSetsRunNoChanges()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
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
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths = [],
            UnchangedFilePaths =
                [".env"]
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("no changes (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run - changes"</remarks>
    [Fact]
    public void TestSetCallsSetsRunChanges()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
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
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths =
            [
                ".env"
            ],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO with encryption (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestSetCallsSetsRunMissingEnvFile()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
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
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs, ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.WarningMessages.ShouldContain("MISSING_ENV_FILE");
        MockLogger.InfoMessages.ShouldContain("? add one with [echo \"HELLO=World\" > .env] and re-run [dotenvx set]");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run - OTHER_ERROR"</remarks>
    [Fact]
    public void TestSetCallsSetsRunOtherError()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
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
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs, ChangedFilePaths = [], UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.WarningMessages.ShouldContain("some error");
        MockLogger.InfoMessages.ShouldContain("some help");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run - changes"</remarks>
    [Fact]
    public void TestSetCallsSetsRunChangesWithLogger()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
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
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths =
            [
                ".env"
            ],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO with encryption (.env)");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "set calls Sets.run - privateKeyAdded and not ignoring .env.keys"</remarks>
    [Fact]
    public void TestSetCallsSetsRunPrivateKeyAddedAndNotIgnoringDotenvKeys()
    {
        Mock<Sets> mockSets = new("KEY", "value", Array.Empty<EnvItem>(), true, null!);
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
        mockSets.Setup(s => s.Run()).Returns(new SetsResult
        {
            ProcessedEnvs = processedEnvs,
            ChangedFilePaths =
            [
                ".env"
            ],
            UnchangedFilePaths = []
        });
        Sets.Factory = (_, _, _, _, _) => mockSets.Object;

        SetsOptions options = new() { Logger = MockLogger };

        SetsResult result = Main.Set("HELLO", "World", options);

        mockSets.Verify(s => s.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("✔ set HELLO with encryption (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
        MockLogger.InfoMessages.ShouldContain(
            "⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
        MockLogger.InfoMessages.ShouldContain(
            "⮕  next run [DOTENV_PRIVATE_KEY='1234' dotenvx get HELLO] to test decryption locally");
        result.ShouldNotBeNull();
    }

    /// <remarks>Refer to "get calls Get.run"</remarks>
    [Fact]
    public void TestGetCallsGetRun()
    {
        Mock<Get> mockGet = new("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Setup(g => g.Run())
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        object? result = Main.Get("KEY");

        mockGet.Verify(g => g.Run(), Times.Once);
        result.ShouldBe("value");
    }

    /// <remarks>Refer to "get calls Get.run undefined"</remarks>
    [Fact]
    public void TestGetCallsGetRunUndefined()
    {
        Mock<Get> mockGet = new("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Setup(g => g.Run()).Returns(new GetResult { Parsed = new Dictionary<string, string>(), Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        object? result = Main.Get("KEY");

        mockGet.Verify(g => g.Run(), Times.Once);
        result.ShouldBeNull();
    }

    /// <remarks>Refer to "get calls Get.run with no key"</remarks>
    [Fact]
    public void TestGetCallsGetRunWithNoKey()
    {
        Mock<Get> mockGet = new(null!, Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Setup(g => g.Run())
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        object? result = Main.Get(null);

        mockGet.Verify(g => g.Run(), Times.Once);
        ((IReadOnlyDictionary<string, string>)result!)["KEY"].ShouldBe("value");
    }

    /// <remarks>Refer to "get calls Get.run format eval"</remarks>
    [Fact]
    public void TestGetCallsGetRunFormatEval()
    {
        Mock<Get> mockGet = new(null!, Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Setup(g => g.Run())
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        GetOptions options = new() { Format = "eval" };

        object? result = Main.Get(null, options);

        mockGet.Verify(g => g.Run(), Times.Once);
        result.ShouldBe("KEY=value\n");
    }

    /// <remarks>Refer to "get calls Get.run format shell"</remarks>
    [Fact]
    public void TestGetCallsGetRunFormatShell()
    {
        Mock<Get> mockGet = new(null!, Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Setup(g => g.Run())
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        GetOptions options = new() { Format = "shell" };

        object? result = Main.Get(null, options);

        mockGet.Verify(g => g.Run(), Times.Once);
        result.ShouldBe("KEY=value ");
    }

    /// <remarks>Refer to "get monorepo/apps/backend/.env AND attempt on directory frontend --strict it throws"</remarks>
    [Fact]
    public void TestGetMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictItThrows()
    {
        Dictionary<string, string> processEnv = new();

        GetOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendPath], Strict = true };

        DotenvxException exception = Should.Throw<DotenvxException>(() => Main.Get("HELLO", options));

        exception.Message.ShouldContain(
            $"[MISSING_ENV_FILE] missing tests/monorepo/apps/frontend file ({Path.GetFullPath(FrontendPath)})");
    }

    /// <remarks>Refer to "get with Get.run errors"</remarks>
    [Fact]
    public void TestGetWithGetRunErrors()
    {
        Mock<Get> mockGet = new("KEY", Array.Empty<EnvItem>(), false, false, null!);
        Exception error = new("some error");
        mockGet.Setup(g => g.Run()).Returns(new GetResult
        {
            Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [error]
        });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        GetOptions options = new() { Logger = MockLogger };

        _ = Main.Get("KEY", options);

        mockGet.Verify(g => g.Run(), Times.Once);
        MockLogger.ErrorMessages.ShouldContain("some error");
    }

    /// <remarks>Refer to "get with Get.run undefined errors"</remarks>
    [Fact]
    public void TestGetWithGetRunUndefinedErrors()
    {
        Mock<Get> mockGet = new("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Setup(g => g.Run())
            .Returns(new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        GetOptions options = new() { Logger = MockLogger };

        _ = Main.Get("KEY", options);

        mockGet.Verify(g => g.Run(), Times.Once);
        MockLogger.ErrorMessages.ShouldBeEmpty();
    }

    /// <remarks>Refer to "get with Get.run errors and ignore"</remarks>
    [Fact]
    public void TestGetWithGetRunErrorsAndIgnore()
    {
        Mock<Get> mockGet = new("KEY", Array.Empty<EnvItem>(), false, false, null!);
        DotenvxException error = new("SOME_ERROR", "some error");
        mockGet.Setup(g => g.Run()).Returns(new GetResult
        {
            Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [error]
        });
        Get.Factory = (_, _, _, _, _) => mockGet.Object;

        GetOptions options = new() { Logger = MockLogger, Ignore = ["SOME_ERROR"] };

        _ = Main.Get("KEY", options);

        mockGet.Verify(g => g.Run(), Times.Once);
        MockLogger.ErrorMessages.ShouldBeEmpty();
    }
}