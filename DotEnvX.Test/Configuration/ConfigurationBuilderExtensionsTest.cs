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
        configuration["HELLO"].ShouldBe("World");
    }
}