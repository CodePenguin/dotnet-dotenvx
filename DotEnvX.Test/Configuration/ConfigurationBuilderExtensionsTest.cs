using Dotenvx.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Dotenvx.Test.Configuration;

public class ConfigurationBuilderExtensionsTest
{
    [Fact]
    public void AddDotEnvX_AddsDotEnvXConfigurationSourceToList()
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.UseDotenvx();
        DotenvxConfigurationSource? dotEnvXSource = configurationBuilder.Sources.OfType<DotenvxConfigurationSource>().FirstOrDefault();
        dotEnvXSource.ShouldNotBeNull();
    }

    [Fact]
    public void AddDotEnvX_DefaultEnvFile()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx().Build();
        configuration["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void AddDotEnvX_ExpandsVariables()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = [".env.expand"];
        }).Build();
        configuration["BASIC_EXPAND"].ShouldBe("basic");
    }

    [Fact]
    public void AddDotEnvX_LocalFileOverrides()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = [".env.local", ".env"];
        }).Build();
        configuration["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void AddDotEnvX_MissingFile_DoesNotThrow()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().UseDotenvx(options =>
        {
            options.Path = ["nonexistent.env"];
        }).Build();
        configuration.AsEnumerable().ShouldBeEmpty();
    }
}