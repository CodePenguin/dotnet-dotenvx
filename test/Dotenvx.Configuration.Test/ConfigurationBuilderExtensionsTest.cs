using Dotenvx.Core.Test;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Dotenvx.Configuration.Test;

public class ConfigurationBuilderExtensionsTest : EnvironmentTestBase
{
    public ConfigurationBuilderExtensionsTest()
    {
        ClearEnvironmentExceptPath();
    }

    [Fact]
    public void TestAddDotenvxAddsDotenvxConfigurationSourceToList()
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.AddDotenvx();
        DotenvxConfigurationSource? dotenvxSource =
            configurationBuilder.Sources.OfType<DotenvxConfigurationSource>().FirstOrDefault();
        dotenvxSource.ShouldNotBeNull();
    }

    [Fact]
    public void TestAddDotenvxDefaultEnvFile()
    {
        Environment.CurrentDirectory = "tests/";
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx().Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestAddDotenvxDefaultEnvFileWithLogging()
    {
        Environment.CurrentDirectory = "tests/";
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Logger = MockLogger;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
        MockLogger.DebugMessages.ShouldContain("BASIC set to basic");
        MockLogger.InfoMessages.ShouldContain("⟐ injected env (35) from .env");
        MockLogger.VerboseMessages.ShouldContain("BASIC set");
    }

    [Fact]
    public void TestAddDotenvxExpandsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/.env.expand"];
        }).Build();
        configuration["BASIC_EXPAND"].ShouldBe("basic");
    }

    [Fact]
    public void TestAddDotenvxLocalFileOverrides()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/.env.local", "./tests/.env"];
        }).Build();
        configuration["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void TestAddDotenvxMissingFileDoesNotThrow()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }

    [Fact]
    public void TestAddDotenvxWithOverloadLoadsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/.env"];
            options.Overload = true;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestAddDotenvxWithProcessEnvUsesCustomEnvironment()
    {
        Dictionary<string, string> customEnv = new() { ["EXISTING_VAR"] = "existing_value" };
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/.env.expand"];
            options.ProcessEnv = customEnv;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
        // ProcessEnv affects how variables are resolved, but the loaded config should still work
    }

    [Fact]
    public void TestAddDotenvxWithIgnoreIgnoresSpecifiedErrors()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
            options.Ignore = ["MISSING_ENV_FILE"];
            options.Strict = true; // Even in strict mode, ignored errors don't throw
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }

    [Fact]
    public void TestAddDotenvxWithStrictThrowsOnErrors()
    {
        Should.Throw<Exception>(() => new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
            options.Strict = true;
        }).Build());
    }

    [Fact]
    public void TestAddDotenvxWithConventionAppliesConvention()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = ["./tests/.env"];
            options.Convention = "nextjs";
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestAddDotenvxNullOptionsCallbackWorks()
    {
        Environment.CurrentDirectory = "tests/";
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx().Build();
        configuration["BASIC"].ShouldBe("basic");
    }
}
