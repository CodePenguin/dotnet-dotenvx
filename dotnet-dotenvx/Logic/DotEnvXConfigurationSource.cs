using Microsoft.Extensions.Configuration;

namespace dotnet_dotenvx.Logic;

public class DotEnvXConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DotEnvXConfigurationProvider();
    }
}