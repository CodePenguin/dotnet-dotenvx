using Microsoft.Extensions.Configuration;

namespace dotnet_dotenvx.Configuration;

public class DotEnvXConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotEnvXConfigurationProvider();
    }
}