using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/buildCommandEnvs.js. Lets a bare directory (not just
/// a file) be passed as an env-file argument: `dotenvx run -f ./config -- cmd` resolves `./config`
/// to `./config/.env` (or, with --convention, to each convention file joined under that
/// directory) via [[ResolveDirectoryFilepath]]. Used by run/get/validate's CLI actions - confirmed
/// 2026-09-24 that none of RunAction.cs/GetAction.cs/ValidateAction.cs implemented this; they used
/// an older, simpler "prepend Conventions() unconditionally, then append explicit items" pattern
/// that never resolved a directory argument at all.
/// </remarks>
internal static partial class Helpers
{
    public static EnvItem[] BuildCommandEnvs(EnvItem[] envs, string? convention)
    {
        List<EnvItem> resolvedEnvs = [];
        bool hasDirectory = false;

        foreach (EnvItem env in envs)
        {
            if (env.Type != EnvType.EnvFile)
            {
                resolvedEnvs.Add(env);
                continue;
            }

            string envFilepath = ResolveDirectoryFilepath(env.Value, ".env");
            if (envFilepath == env.Value)
            {
                resolvedEnvs.Add(env);
                continue;
            }

            hasDirectory = true;
            if (!string.IsNullOrEmpty(convention))
            {
                foreach (EnvItem conventionEnv in Conventions(convention))
                {
                    resolvedEnvs.Add(new EnvItem
                    {
                        Type = conventionEnv.Type,
                        Value = ResolveDirectoryFilepath(env.Value, conventionEnv.Value)
                    });
                }
            }
            else
            {
                resolvedEnvs.Add(new EnvItem { Type = env.Type, Value = envFilepath });
            }
        }

        if (!string.IsNullOrEmpty(convention) && !hasDirectory)
        {
            return [.. Conventions(convention), .. resolvedEnvs];
        }

        return [.. resolvedEnvs];
    }
}
