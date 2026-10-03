namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/cryptography/mutateKeysSrc.js: appends a private key (under a
/// `# comment` line) to a keys file source, starting the standard header when the source is empty.
/// </remarks>
public static partial class Helpers
{
    private static readonly string FirstTimeKeysSrc = string.Join("\n",
        "#/------------------!DOTENV_PRIVATE_KEYS!-------------------/",
        "#/ private decryption keys. DO NOT commit to source control /",
        "#/     [how it works](https://dotenvx.com/encryption)       /",
        "#/          ⛨ ARMORED KEYS: `dotenvx armor up`              /",
        "#/----------------------------------------------------------/");

    public static string MutateKeysSrc(string? keysSrc, string privateKeyName, string privateKeyValue,
        string comment)
    {
        string appendPrivateKey = string.Join("\n", $"# {comment}", $"{privateKeyName}={privateKeyValue}", "");

        if (string.IsNullOrEmpty(keysSrc))
        {
            keysSrc = $"{FirstTimeKeysSrc}\n";
        }

        return $"{keysSrc}\n{appendPrivateKey}";
    }
}
