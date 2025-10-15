using Dotenvx.Core.Interop.Dotenvx.Types;

using Microsoft.Extensions.Configuration;

namespace Dotenvx.Core.Configuration;

public class DotenvxConfigurationProvider(DotenvxConfigurationSource source) : ConfigurationProvider
{
    public override void Load()
    {
        Dictionary<string, string?> variables = new();
        ConfigOptions options = new();
        source.Options?.Invoke(options);
        ConfigResult result = Interop.Dotenvx.Helpers.Dotenvx.Config(options);
        foreach (KeyValuePair<string, string> keyValuePair in result.Parsed)
        {
            variables.Add(keyValuePair.Key, keyValuePair.Value);
        }

        Data = variables;
    }
}