namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `derive(privateKeyHex)` export (checked live): the compressed
/// public key hex for a private key hex. Throws for an invalid private key, like the original.
/// </remarks>
public static partial class Primitives
{
    public static string Derive(string privateKeyHex)
    {
        return Interop.EciesJs.PrivateKey.FromHex(privateKeyHex).PublicKey.ToHex();
    }
}
