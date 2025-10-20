using Dotenvx.Core.Interop.EciesJs;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static (string PublicKey, string PrivateKey) DeriveKeyPair(string? existingPrivateKey = null)
    {
        PrivateKey kp = existingPrivateKey != null ? PrivateKey.FromHex(existingPrivateKey) : new PrivateKey();
        string publicKey = Convert.ToHexString(kp.GetPublicKey());
        string privateKey = kp.ToHex();
        return (publicKey, privateKey);
    }
}