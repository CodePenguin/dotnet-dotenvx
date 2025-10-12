using Microsoft.Extensions.Configuration;

namespace Dotenvx.Core.Configuration;

public class DotenvxConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotenvxConfigurationProvider();
    }
}