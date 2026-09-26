namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/cli/dotenvx.js's `collectEnvs('envFile')` - a single -f/--env-file
/// token can itself be a comma-separated list of paths (e.g. `-f .env,.env.production`), split and
/// trimmed before being treated as separate env files:
/// `value.split(',').map(item => item.trim()).filter(Boolean)`. System.CommandLine's
/// `Option&lt;string[]?&gt;` already supports repeated/space-separated multi-value syntax natively,
/// but has no concept of comma-splitting a single token - found missing 2026-09-27 via a flag-
/// combination sweep (`-f .env,.env.prod` silently looked for one file literally named
/// ".env,.env.prod" instead of two). Applies to every action whose CLI registration uses
/// `collectEnvs('envFile')` at this pin: run/get/set/del/encrypt/decrypt/validate (confirmed via
/// direct grep of dotenvx.js) - NOT `ls`/`genexample`/`ext *`, which use plain commander
/// `&lt;paths...&gt;` variadic options with no comma-splitting at all, and NOT `keypair`, which
/// only supports a single `-f` file in this port currently regardless (a separate, already-known
/// gap - see KeyPairAction.cs).
/// </remarks>
public static partial class Helpers
{
    public static string[] SplitCommaSeparatedEnvFiles(string[]? envFile)
    {
        if (envFile == null)
        {
            return [];
        }

        return envFile
            .SelectMany(value => value.Split(','))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
    }
}
