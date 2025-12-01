using Dotenvx.Interop.EciesJs;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/keyPair.js</remarks>
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