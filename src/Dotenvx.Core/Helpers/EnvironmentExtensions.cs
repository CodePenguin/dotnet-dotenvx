using System.Collections;

namespace Dotenvx.Helpers;

public static class EnvironmentHelper
{
    public static IDictionary<string, string> GetEnvironmentVariablesAsDictionary()
    {
        return new Dictionary<string, string>(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
    }
}