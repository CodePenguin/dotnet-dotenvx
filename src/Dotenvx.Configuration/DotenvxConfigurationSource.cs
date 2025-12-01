using Dotenvx.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Configuration;

namespace Dotenvx.Configuration;

public class DotenvxConfigurationSource(Action<ConfigOptions>? options = null) : IConfigurationSource
{
    public Action<ConfigOptions>? Options { get; } = options;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotenvxConfigurationProvider(this);
    }
}