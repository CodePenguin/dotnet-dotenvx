using Dotenvx.Core.Interop.EciesJs;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static (string PublicKey, string PrivateKey) KeyPair(string? existingPrivateKey = null)
    {
        PrivateKey kp = existingPrivateKey != null
            ? PrivateKey.FromHex(existingPrivateKey)
            : new PrivateKey();
        return (kp.PublicKey.ToHex(), kp.ToHex());
    }
}