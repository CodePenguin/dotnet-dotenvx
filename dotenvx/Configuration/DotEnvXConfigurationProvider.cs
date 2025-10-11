using Microsoft.Extensions.Configuration;

namespace dotenvx.Configuration;

public class DotenvxConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        Dictionary<string, string?> variables = new();
        Data = variables;
    }
}