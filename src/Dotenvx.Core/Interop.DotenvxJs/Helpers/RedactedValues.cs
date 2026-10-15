using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactedValues.js. Without an Envfile nothing is redacted unless `--redact` asked for
/// the legacy rule (every injected value except the ones of public keys); with an Envfile each key's own `stdio.redacted` decides
/// (redacted unless the Envfile says otherwise), including the values of Envfile keys that are only in the environment.
/// </remarks>
public static partial class Helpers
{
    /// <summary>`isPublicKey(name)`.</summary>
    public static bool IsPublicKeyName(string name)
    {
        return name.Contains("PUBLIC", StringComparison.Ordinal) || name.StartsWith("VITE", StringComparison.Ordinal);
    }

    public static string[] RedactedValues(IEnumerable<RunProcessedEnv> processedEnvs, EnvfileSchema? schema = null,
        IDictionary<string, string>? processEnv = null, bool legacyRedact = false)
    {
        bool exists = schema?.Exists == true;
        if (!exists && !legacyRedact)
        {
            return [];
        }

        processEnv ??= new Dictionary<string, string>();
        HashSet<string> result = [];
        List<(IReadOnlyDictionary<string, string> Injected, IReadOnlyDictionary<string, string> PreExisted)> rows =
            [.. processedEnvs.Select(row => (row.Injected, row.PreExisted))];
        if (exists)
        {
            rows.Add((schema!.Keys.Keys.Where(processEnv.ContainsKey).ToDictionary(key => key, key => processEnv[key]),
                new Dictionary<string, string>()));
        }

        foreach ((IReadOnlyDictionary<string, string> injected, IReadOnlyDictionary<string, string> preExisted) in rows)
        {
            Dictionary<string, string> values = new(injected);
            foreach ((string key, string value) in preExisted)
            {
                values[key] = value;
            }

            foreach ((string key, string value) in values)
            {
                bool redacted = exists
                    ? !schema!.Keys.TryGetValue(key, out global::Dotenvx.Primitives.KeyPolicy? policy) || policy.Redacted
                    : !IsPublicKeyName(key);
                if (!redacted || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                result.Add(value);
            }
        }

        return [.. result];
    }
}
