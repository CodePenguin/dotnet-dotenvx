using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactedValues.js. Typed specifically to
/// `RunProcessedEnv` rather than a generic processedEnv shape - `run` is the only command that
/// calls this at this pin (confirmed via grep of every `redactedValues`/`redactOutput` call site
/// in the submodule).
/// </remarks>
public static partial class Helpers
{
    public static string[] RedactedValues(IEnumerable<RunProcessedEnv> processedEnvs)
    {
        HashSet<string> result = [];

        foreach (RunProcessedEnv processedEnv in processedEnvs)
        {
            Dictionary<string, string> values = new(processedEnv.Injected);
            foreach ((string key, string value) in processedEnv.PreExisted)
            {
                values[key] = value;
            }

            foreach ((string key, string value) in values)
            {
                if (IsPlainKey(key))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                result.Add(value);
            }
        }

        return result.ToArray();
    }
}
