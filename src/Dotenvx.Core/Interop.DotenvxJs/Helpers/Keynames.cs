using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/conventions/keynames.js. A public key name found in the source wins over the file name.</remarks>
public static partial class Helpers
{
    public static (string PublicKeyName, string PrivateKeyName) Keynames(string filepath = ".env", string src = "")
    {
        foreach (string name in Primitives.Primitives.Scan(src, new ScanOptions()).Parsed.Keys)
        {
            if (name.StartsWith("DOTENV_PUBLIC_KEY", StringComparison.Ordinal))
            {
                return (name, "DOTENV_PRIVATE_KEY" + name["DOTENV_PUBLIC_KEY".Length..]);
            }
        }

        return (GuessPublicKeyName(filepath), GuessPrivateKeyName(filepath));
    }
}
