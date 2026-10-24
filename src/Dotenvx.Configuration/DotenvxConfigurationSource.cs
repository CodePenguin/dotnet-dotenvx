using Microsoft.Extensions.Configuration;

namespace Dotenvx.Configuration;

public class DotenvxConfigurationSource : IConfigurationSource
{
    /// <summary>Sets the same <see cref="ConfigOptions" /> as <c>Dotenvx.Config</c> (<c>Path</c>, <c>Overload</c>, <c>Strict</c>, ...).</summary>
    public Action<ConfigOptions>? ConfigureOptions { get; set; }

    /// <summary>Reloads the configuration when one of the <c>Path</c> files (default <c>.env</c>) changes.</summary>
    public bool ReloadOnChange { get; set; }

    /// <summary>Milliseconds to wait after a change before reloading (editors often write a file in several steps).</summary>
    public int ReloadDelay { get; set; } = 250;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotenvxConfigurationProvider(this);
    }
}
