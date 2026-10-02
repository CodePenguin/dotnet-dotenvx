using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No current upstream reference exists - smartDotenvPublicKey.js's process.env-aware search was
/// deleted from dotenvx entirely well before the v2.24.1 pin (confirmed via the submodule's own git
/// history: renamed to smartPublicKey.js, then gutted, then removed outright - both are ancestors of
/// the v2.24.1 tag). The real current replacement is `resolvers/keypair.js`'s `publickeys(src)[0]`
/// (a @dotenvx/primitives export), which reads EXCLUSIVELY from the target .env file's own content -
/// there is no process.env fallback anywhere in current upstream for the PUBLIC key (unlike the
/// private key, where SmartDotenvPrivateKey.cs's own SearchProcessEnv step is still real and
/// correct). Confirmed live this port's previous process.env-first step was a real, serious bug: a
/// stray DOTENV_PUBLIC_KEY/DOTENV_PUBLIC_KEY_&lt;ENV&gt; left in the shell environment (e.g. from an
/// earlier `dotenvx run` in the same session) made `encrypt`/`set` on a brand-new .env file either
/// crash outright (an invalid EC point) or silently encrypt against the wrong public key while
/// skipping real key generation - not merely cosmetic.
/// </remarks>
public static partial class Helpers
{
    public static string? SmartDotenvPublicKey(string envFilePath)
    {
        string publicKeyName = GuessPublicKeyName(envFilePath); // DOTENV_PUBLIC_KEY_${ENVIRONMENT}
        return SearchEnvFile(publicKeyName, envFilePath);
    }

    private static string? SearchEnvFile(string publicKeyName, string envFilePath)
    {
        if (!Fsx.ExistsSync(envFilePath))
        {
            return null;
        }

        string keysSrc = Fsx.ReadFileX(envFilePath);
        Dictionary<string, object> keysParsed = Parse(keysSrc);

        if (keysParsed.TryGetValue(publicKeyName, out object? value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return null;
    }
}