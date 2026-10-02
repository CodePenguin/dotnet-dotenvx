using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: @dotenvx/primitives' `encrypt(publicKey, value)` export (no readable source
/// exists - encryptValue.js, this file's old reference, was deleted from dotenvx itself well before
/// the v2.24.1 pin once its logic moved there). Verified via the live-oracle technique: every real
/// call site (transforms/encrypt.js, transforms/set.js) calls it with no options, i.e. always with
/// primitives' default `prefix: true` behavior - confirmed round-trip interoperable with the real
/// Node CLI.
/// </remarks>
public static partial class Helpers
{
    private const string Prefix = "encrypted:";

    public static string EncryptValue(string value, string publicKey)
    {
        byte[] ciphertext = EciesJs.EciesJs.Encrypt(publicKey, Encoding.UTF8.GetBytes(value));
        string encoded = Convert.ToBase64String(ciphertext);
        return $"{Prefix}{encoded}";
    }
}