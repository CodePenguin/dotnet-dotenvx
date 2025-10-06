using dotnet_dotenvx.Logic;
using Microsoft.Extensions.Configuration;

namespace dotnet_dotenvx;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder UseDotEnvX(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new DotEnvXConfigurationSource());
        return configurationBuilder;
    }
}