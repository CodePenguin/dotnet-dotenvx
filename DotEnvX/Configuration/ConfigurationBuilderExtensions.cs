using Microsoft.Extensions.Configuration;

namespace DotEnvX.Configuration;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder UseDotEnvX(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new DotEnvXConfigurationSource());
        return configurationBuilder;
    }
}