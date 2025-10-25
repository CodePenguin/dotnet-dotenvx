using Dotenvx.Core.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Dotenvx.Configuration.Test;

public class ConfigurationBuilderExtensionsTest : EnvironmentTestBase
{
    [Fact]
    public void UseDotenvx_AddsDotenvxConfigurationSourceToList()
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.UseDotenvx();
        DotenvxConfigurationSource? dotenvxSource =
            configurationBuilder.Sources.OfType<DotenvxConfigurationSource>().FirstOrDefault();
        dotenvxSource.ShouldNotBeNull();
    }

    [Fact]
    public void UseDotenvx_DefaultEnvFile()
    {
        string originalDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = "tests/";
            IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx().Build();
            configuration["BASIC"].ShouldBe("basic");
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    [Fact]
    public void UseDotenvx_ExpandsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env.expand"];
        }).Build();
        configuration["BASIC_EXPAND"].ShouldBe("basic");
    }

    [Fact]
    public void UseDotenvx_LocalFileOverrides()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env.local", "./tests/.env"];
        }).Build();
        configuration["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void UseDotenvx_MissingFile_DoesNotThrow()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }

    [Fact]
    public void UseDotenvx_WithOverload_LoadsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env"];
            options.Overload = true;
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void UseDotenvx_WithProcessEnv_UsesCustomEnvironment()
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
    public void UseDotenvx_WithIgnore_IgnoresSpecifiedErrors()
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
    public void UseDotenvx_WithStrict_ThrowsOnErrors()
    {
        Should.Throw<Exception>(() => new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/nonexistent.env"];
            options.Strict = true;
        }).Build());
    }

    [Fact]
    public void UseDotenvx_WithConvention_AppliesConvention()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["./tests/.env"];
            options.Convention = "nextjs";
        }).Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void UseDotenvx_NullOptionsCallback_Works()
    {
        string originalDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = "tests/";
            Action<ConfigOptions?>? callback = null;
            IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(callback).Build();
            configuration["BASIC"].ShouldBe("basic");
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }
}