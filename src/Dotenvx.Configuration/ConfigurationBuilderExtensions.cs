using Dotenvx.Core.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Configuration;

namespace Dotenvx.Configuration;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder UseDotenvx(this IConfigurationBuilder configurationBuilder,
        Action<ConfigOptions>? options = null)
    {
        configurationBuilder.Add(new DotenvxConfigurationSource(options));
        return configurationBuilder;
    }
}