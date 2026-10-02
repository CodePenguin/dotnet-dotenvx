using Dotenvx.Interop.EciesJs;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: @dotenvx/primitives' `keypair(existingPrivateKeyHex?)` export (no readable
/// source exists - keyPair.js, this file's old reference, was deleted from dotenvx itself well
/// before the v2.24.1 pin once its logic moved there). Verified via live-oracle decompile: if a
/// private key is given (JS-truthy - an empty string counts as "not given"), derive and return its
/// public key; otherwise generate a fresh keypair. Every real call site (transforms/encrypt.js,
/// transforms/set.js) only ever calls the no-arg fresh-key path, matching this port's own callers.
/// </remarks>
public static partial class Helpers
{
    public static (string PublicKey, string PrivateKey) KeyPair(string? existingPrivateKey = null)
    {
        PrivateKey kp = !string.IsNullOrEmpty(existingPrivateKey)
            ? PrivateKey.FromHex(existingPrivateKey)
            : new PrivateKey();
        return (kp.PublicKey.ToHex(), kp.ToHex());
    }
}