using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/envResolution/determine.js (moved there from
/// src/lib/helpers/determineEnvs.js, which no longer exists at this pin - confirmed 2026-09-24).
/// </remarks>
internal static partial class Helpers
{
    private static readonly EnvItem[] DefaultEnvs = [new() { Type = EnvType.EnvFile, Value = ".env" }];

    public static EnvItem[] DetermineEnvs(EnvItem[]? envs, IDictionary<string, string> processEnv)
    {
        string[] privateKeyNames = DotenvPrivateKeyNames(processEnv);
        EnvItem[] defaults = privateKeyNames.Length != 0
            ? DetermineEnvsFromDotenvPrivateKey(privateKeyNames)
            : DefaultEnvs;

        if (envs == null || envs.Length == 0)
        {
            return [.. defaults];
        }

        bool fileAlreadySpecified = envs.Any(env => env.Type == EnvType.EnvFile);

        if (fileAlreadySpecified)
        {
            return envs;
        }

        // Same `defaults` (private-key-derived filenames when DOTENV_PRIVATE_KEY* is set, else
        // plain ".env") as the no-envs-at-all branch above - JS computes `defaults` once up front
        // and reuses it in both places. This branch previously hardcoded DefaultEnvs here instead,
        // silently ignoring DOTENV_PRIVATE_KEY* whenever the caller passed only --env strings with
        // no -f (confirmed 2026-09-24, no existing test exercised this combination).
        return [.. defaults, .. envs];
    }

    private static EnvItem[] DetermineEnvsFromDotenvPrivateKey(string[] privateKeyNames)
    {
        List<EnvItem> envs = [];
        foreach (string privateKeyName in privateKeyNames)
        {
            string filename = GuessPrivateKeyFilename(privateKeyName);
            envs.Add(new EnvItem { Type = EnvType.EnvFile, Value = filename });
        }

        return [.. envs];
    }
}
