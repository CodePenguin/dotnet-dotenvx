using System.Text;

namespace Dotenvx.Primitives;

/// <remarks>
/// Reference source: @dotenvx/primitives' `encrypt(publicKey, value)` export (no readable source
/// exists - encryptValue.js, this file's old reference, was deleted from dotenvx itself well before
/// the v2.24.1 pin once its logic moved there). Verified via the live-oracle technique: every real
/// call site (transforms/encrypt.js, transforms/set.js) calls it with no options, i.e. always with
/// primitives' default `prefix: true` behavior - confirmed round-trip interoperable with the real
/// Node CLI. Parameter order here is (value, publicKey), NOT upstream's (publicKey, value) -
/// deliberately left as this port's own existing order rather than reordered during the 2026-10-02
/// project split, since both parameters are type `string` and a silent argument-swap bug at a call
/// site wouldn't be caught by the compiler.
/// </remarks>
public static partial class Primitives
{
    // public (not private) because Dotenvx.Core's DecryptKeyValue.cs - the dotenvx-own wrapper
    // around this project's Decrypt, not a primitives export itself, so it stays in Core - needs
    // the same prefix string.
    public const string Prefix = "encrypted:";

    public static string Encrypt(string value, string publicKey)
    {
        byte[] ciphertext = Interop.EciesJs.EciesJs.Encrypt(publicKey, Encoding.UTF8.GetBytes(value));
        string encoded = Convert.ToBase64String(ciphertext);
        return $"{Prefix}{encoded}";
    }
}
