using Dotenvx.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Dotenvx.Test.Configuration;

public class ConfigurationBuilderExtensionsTest : EnvironmentTestBase
{
    [Fact]
    public void UseDotenvx_AddsDotenvxConfigurationSourceToList()
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.UseDotenvx();
        DotenvxConfigurationSource? dotenvxSource = configurationBuilder.Sources.OfType<DotenvxConfigurationSource>().FirstOrDefault();
        dotenvxSource.ShouldNotBeNull();
    }

    [Fact]
    public void UseDotenvx_DefaultEnvFile()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx().Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void UseDotenvx_ExpandsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = [".env.expand"];
        }).Build();
        configuration["BASIC_EXPAND"].ShouldBe("basic");
    }

    [Fact]
    public void UseDotenvx_LocalFileOverrides()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = [".env.local", ".env"];
        }).Build();
        configuration["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void UseDotenvx_MissingFile_DoesNotThrow()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["nonexistent.env"];
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }
}