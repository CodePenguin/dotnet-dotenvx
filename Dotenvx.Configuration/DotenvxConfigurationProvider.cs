using Dotenvx.Core.Interop.DotenvxJs;
using Dotenvx.Core.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Configuration;

namespace Dotenvx.Configuration;

public class DotenvxConfigurationProvider(DotenvxConfigurationSource source) : ConfigurationProvider
{
    public override void Load()
    {
        Dictionary<string, string?> variables = new();
        ConfigOptions options = new();
        source.Options?.Invoke(options);
        ConfigResult result = Main.Config(options);
        foreach (KeyValuePair<string, string> keyValuePair in result.Parsed)
        {
            variables.Add(keyValuePair.Key, keyValuePair.Value);
        }

        Data = variables;
    }
}