using Microsoft.Extensions.Configuration;

namespace dotenvx.Configuration;

public class DotenvxConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotenvxConfigurationProvider();
    }
}