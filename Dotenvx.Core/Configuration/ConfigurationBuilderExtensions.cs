using Microsoft.Extensions.Configuration;

namespace Dotenvx.Core.Configuration;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder UseDotenvx(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new DotenvxConfigurationSource());
        return configurationBuilder;
    }
}