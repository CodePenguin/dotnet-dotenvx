using Microsoft.Extensions.Configuration;

namespace Dotenvx.Configuration;

public static class ConfigurationBuilderExtensions
{
    /// <summary>Adds the <c>.env</c> file (encrypted values are decrypted) as a configuration source.</summary>
    public static IConfigurationBuilder AddDotenvx(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Add(new DotenvxConfigurationSource());
    }

    /// <summary>Adds the <c>.env</c> files selected by <paramref name="configureOptions" /> as a configuration source.</summary>
    public static IConfigurationBuilder AddDotenvx(this IConfigurationBuilder builder,
        Action<ConfigOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return builder.AddDotenvx(source => source.ConfigureOptions = configureOptions);
    }

    /// <summary>Adds a <see cref="DotenvxConfigurationSource" /> configured by <paramref name="configureSource" />.</summary>
    public static IConfigurationBuilder AddDotenvx(this IConfigurationBuilder builder,
        Action<DotenvxConfigurationSource> configureSource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureSource);

        DotenvxConfigurationSource source = new();
        configureSource(source);
        return builder.Add(source);
    }
}
