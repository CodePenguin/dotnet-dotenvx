using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Types;

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
    public void TestUseDotenvxAddsDotenvxConfigurationSourceToList()
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.UseDotenvx();
        DotenvxConfigurationSource? dotenvxSource =
            configurationBuilder.Sources.OfType<DotenvxConfigurationSource>().FirstOrDefault();
        dotenvxSource.ShouldNotBeNull();
    }

    [Fact]
    public void TestUseDotenvxDefaultEnvFile()
    {
        Environment.CurrentDirectory = "tests/";
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx().Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestUseDotenvxDefaultEnvFileWithLogging()
    {
        Environment.CurrentDirectory = "tests/";
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Logger = MockLogger;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
        MockLogger.DebugMessages.ShouldContain("BASIC set to basic");
        MockLogger.InfoMessages.ShouldContain("injecting env (35) from .env");
        MockLogger.VerboseMessages.ShouldContain("BASIC set");
    }

    [Fact]
    public void TestUseDotenvxExpandsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env.expand"];
        }).Build();
        configuration["BASIC_EXPAND"].ShouldBe("basic");
    }

    [Fact]
    public void TestUseDotenvxLocalFileOverrides()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env.local", "./tests/.env"];
        }).Build();
        configuration["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void TestUseDotenvxMissingFileDoesNotThrow()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }

    [Fact]
    public void TestUseDotenvxWithOverloadLoadsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env"];
            options.Overload = true;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestUseDotenvxWithProcessEnvUsesCustomEnvironment()
    {
        Dictionary<string, string> customEnv = new() { ["EXISTING_VAR"] = "existing_value" };
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env.expand"];
            options.ProcessEnv = customEnv;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
        // ProcessEnv affects how variables are resolved, but the loaded config should still work
    }

    [Fact]
    public void TestUseDotenvxWithIgnoreIgnoresSpecifiedErrors()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
            options.Ignore = ["MISSING_ENV_FILE"];
            options.Strict = true; // Even in strict mode, ignored errors don't throw
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }

    [Fact]
    public void TestUseDotenvxWithStrictThrowsOnErrors()
    {
        Should.Throw<Exception>(() => new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
            options.Strict = true;
        }).Build());
    }

    [Fact]
    public void TestUseDotenvxWithConventionAppliesConvention()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env"];
            options.Convention = "nextjs";
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestUseDotenvxNullOptionsCallbackWorks()
    {
        Environment.CurrentDirectory = "tests/";
        Action<ConfigOptions?>? callback = null;
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(callback).Build();
        configuration["BASIC"].ShouldBe("basic");
    }
}