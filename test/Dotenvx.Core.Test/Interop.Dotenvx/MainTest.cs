using Dotenvx.Core.Interop.DotenvxJs;
using Dotenvx.Core.Interop.DotenvxJs.Helpers;
using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Types;

using Moq;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx;

public class MainTest : EnvironmentTestBase
{
    private const string BackendEnvFile = "tests/monorepo/apps/backend/.env";
    private const string FrontendEnvFile = "tests/monorepo/apps/frontend/.env";

    private const string EncryptedValue =
        "encrypted:BE9Y7LKANx77X1pv1HnEoil93fPa5c9rpL/1ps48uaRT9zM8VR6mHx9yM+HktKdsPGIZELuZ7rr2mn1gScsmWitppAgE/1lVprNYBCqiYeaTcKXjDUXU5LfsEsflnAsDhT/kWG1l";

    private const string ValidPrivateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c";

    [Fact]
    public void ConfigCallsRunRun()
    {
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigWithConventionCallsRunRunWithProperEnvs()
    {
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions { Convention = "nextjs" });

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigWithConventionFlowCallsRunRunWithProperEnvs()
    {
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions { Convention = "flow" });

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigWithRunRunErrors()
    {
        DotenvxException error = new("GENERIC_ERROR", "some error");
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
                [new ProcessedEnv { Errors = [error] }],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigWithRunRunErrorsAndIgnore()
    {
        DotenvxException error = new("SOME_ERROR", "some error");
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs =
                [new ProcessedEnv { Errors = [error] }],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions { Ignore = ["SOME_ERROR"] });

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
    }

    [Fact]
    public void ParseCallsParseRun()
    {
        Mock<Parse> mockParse = new("", null!, new Dictionary<string, string>(), false, null!);
        mockParse.Setup(p => p.Execute()).Returns(new ParseResult
        {
            Parsed = new Dictionary<string, string> { ["HELLO"] = "World" },
            ProcessEnv = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(),
            PreExisted = new Dictionary<string, string>(),
            Errors = []
        });
        Main.ParseFactory = (_, _, _, _, _) => mockParse.Object;

        IDictionary<string, string> parsed = Main.Parse("HELLO=World");

        mockParse.Verify(p => p.Execute(), Times.Once);
        parsed["HELLO"].ShouldBe("World");
    }

    [Fact]
    public void ParseCallsParseRunWithOptionsProcessEnv()
    {
        Mock<Parse> mockParse = new("", null!, new Dictionary<string, string>(), false, null!);
        mockParse.Setup(p => p.Execute()).Returns(new ParseResult
        {
            Parsed = new Dictionary<string, string> { ["HELLO"] = "World" },
            ProcessEnv = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(),
            PreExisted = new Dictionary<string, string>(),
            Errors = []
        });
        Main.ParseFactory = (_, _, _, _, _) => mockParse.Object;

        ParseOptions options = new() { ProcessEnv = new Dictionary<string, string>() };
        IDictionary<string, string> parsed = Main.Parse("HELLO=World", options);

        mockParse.Verify(p => p.Execute(), Times.Once);
        parsed["HELLO"].ShouldBe("World");
    }

    [Fact]
    public void ParseCallsParseRunWithOptionsPrivateKey()
    {
        Mock<Parse> mockParse = new("", null!, new Dictionary<string, string>(), false, null!);
        mockParse.Setup(p => p.Execute()).Returns(new ParseResult
        {
            Parsed = new Dictionary<string, string> { ["HELLO"] = "World" },
            ProcessEnv = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(),
            PreExisted = new Dictionary<string, string>(),
            Errors = []
        });
        Main.ParseFactory = (_, _, _, _, _) => mockParse.Object;

        ParseOptions options = new() { PrivateKey = ValidPrivateKey };
        IDictionary<string, string> parsed = Main.Parse($"HELLO=\"{EncryptedValue}\"", options);

        mockParse.Verify(p => p.Execute(), Times.Once);
        parsed["HELLO"].ShouldBe("World");
    }

    [Fact]
    public void ParseCallsParseRunWithInvalidOptionsPrivateKey()
    {
        Mock<Parse> mockParse = new("", null!, new Dictionary<string, string>(), false, null!);
        mockParse.Setup(p => p.Execute()).Returns(new ParseResult
        {
            Parsed = new Dictionary<string, string> { ["HELLO"] = EncryptedValue },
            ProcessEnv = new Dictionary<string, string>(),
            Injected = new Dictionary<string, string>(),
            PreExisted = new Dictionary<string, string>(),
            Errors = []
        });
        Main.ParseFactory = (_, _, _, _, _) => mockParse.Object;

        ParseOptions options = new() { PrivateKey = "12345" };
        IDictionary<string, string> parsed = Main.Parse($"HELLO=\"{EncryptedValue}\"", options);

        mockParse.Verify(p => p.Execute(), Times.Once);
        parsed["HELLO"].ShouldBe(EncryptedValue);
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnv()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAlreadySet()
    {
        Dictionary<string, string> processEnv = new() { ["HELLO"] = "world" };
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("world");
        result.Parsed["HELLO"].ShouldBe("world");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAlreadySetOverload()
    {
        Dictionary<string, string> processEnv = new() { ["HELLO"] = "world" };
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile], Overload = true };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAndFrontendEnv()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { ProcessEnv = processEnv, Path = [BackendEnvFile, FrontendEnvFile] };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAndFrontendEnvOverload()
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

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAndFrontendMissing()
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

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontend()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv, Path = [BackendEnvFile, "tests/monorepo/apps/frontend"]
        };

        ConfigResult result = Main.Config(options);

        processEnv["HELLO"].ShouldBe("backend");
        result.Parsed["HELLO"].ShouldBe("backend");
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldContain("MISSING_ENV_FILE");
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictItThrows()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv, Path = [BackendEnvFile, "tests/monorepo/apps/frontend"], Strict = true
        };

        DotenvxException exception = Should.Throw<DotenvxException>(() => Main.Config(options));
        exception.Message.ShouldContain("MISSING_ENV_FILE");
    }

    [Fact]
    public void ConfigMonorepoAppsBackendEnvAndAttemptOnDirectoryFrontendStrictButErrorAlsoIgnored()
    {
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new()
        {
            ProcessEnv = processEnv,
            Path = [BackendEnvFile, "tests/monorepo/apps/frontend"],
            Strict = true,
            Ignore = ["MISSING_ENV_FILE"]
        };

        ConfigResult result = Main.Config(options);

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigRadarObserveThrowsErrorButConfigContinuesToWork()
    {
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void ConfigOpsObserveThrowsErrorButConfigContinuesToWork()
    {
        Mock<Run> mockRun = new(new List<EnvItem>(), false, new Dictionary<string, string>(), null!, false);
        mockRun.Setup(r => r.Execute()).Returns(new RunResult
        {
            ProcessedEnvs = [],
            ReadableStrings = [],
            ReadableFilePaths = [],
            UniqueInjectedKeys = [],
            BeforeEnv = new Dictionary<string, string>(),
            AfterEnv = new Dictionary<string, string>()
        });
        Main.RunFactory = (_, _, _, _) => mockRun.Object;

        ConfigResult result = Main.Config(new ConfigOptions());

        mockRun.Verify(r => r.Execute(), Times.Once);
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }
}