using Microsoft.Extensions.Configuration;
using dotnet_dotenvx.Logic;

namespace dotnet_dotenvx;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder UseDotEnvX(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new DotEnvXConfigurationSource());
        return configurationBuilder;
    }
}