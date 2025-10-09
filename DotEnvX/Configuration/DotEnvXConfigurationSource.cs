using Microsoft.Extensions.Configuration;

namespace DotEnvX.Configuration;

public class DotEnvXConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotEnvXConfigurationProvider();
    }
}