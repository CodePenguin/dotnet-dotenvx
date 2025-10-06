using System;
using NBitcoin.Secp256k1;

namespace dotnet_dotenvx.Logic;

public static class EciesJsPublicKey
{
    /// <summary>
    /// C# equivalent for decapsulate function from https://github.com/ecies/js/blob/master/src/keys/PublicKey.ts
    /// </summary>
    public static byte[] Decapsulate(byte[] publicKey, byte[] privateKey, bool compressed = false)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(publicKey);
        if (privateKey.Length != 32) throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));
        
        var ctx = Context.Instance;

        // create EC private key object (checks scalar validity)
        if (!ctx.TryCreateECPrivKey(privateKey, out var ecPrivateKey))
        {
            throw new ArgumentException("Invalid private key", nameof(privateKey));
        }

        // Parse Compressed/Uncompressed Public Key 
        if (!ECPubKey.TryCreate(publicKey, ctx, out _, out var ecPublicKey))
        {
            throw new ArgumentException("Invalid public key", nameof(publicKey));
        }

        // Get Shared Public Key
        var sharedPoint = ecPublicKey.GetSharedPubkey(ecPrivateKey);

        return EciesJsUtils.GetSharedKey(ecPublicKey.ToBytes(compressed), sharedPoint.ToBytes(compressed));
    }
}