using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/maskProcessedEnvs.js. Used by Main.Config() (real
/// config()'s `if (options.mask !== undefined) maskProcessedEnvs(processedEnvs, processEnv,
/// showChar)`) - found missing entirely 2026-09-29 via a code audit (Main.Config() had no mask
/// support at all). Masks every resolver-reporting bucket (parsed/injected/preExisted) on each
/// processed env IN PLACE, then also masks the destination `processEnv` (the actual environment
/// dictionary values get injected into) for every key that was resolved from ANY source - so a
/// variable that already pre-existed with a real value gets its live value overwritten with the
/// masked form too, not just what gets logged/returned.
/// </remarks>
public static partial class Helpers
{
    public static void MaskProcessedEnvs(
        IEnumerable<RunProcessedEnv> processedEnvs, IDictionary<string, string> processEnv, object? showChar)
    {
        HashSet<string> resolvedKeys = [];

        foreach (RunProcessedEnv processedEnv in processedEnvs)
        {
            foreach (string key in processedEnv.Parsed.Keys)
            {
                resolvedKeys.Add(key);
            }

            processedEnv.Parsed = processedEnv.Parsed.ToDictionary(kv => kv.Key, kv => Mask(kv.Value, showChar));
            processedEnv.Injected = processedEnv.Injected.ToDictionary(kv => kv.Key, kv => Mask(kv.Value, showChar));
            processedEnv.PreExisted =
                processedEnv.PreExisted.ToDictionary(kv => kv.Key, kv => Mask(kv.Value, showChar));
        }

        foreach (string key in resolvedKeys)
        {
            if (processEnv.TryGetValue(key, out string? value))
            {
                processEnv[key] = Mask(value, showChar);
            }
        }
    }
}
