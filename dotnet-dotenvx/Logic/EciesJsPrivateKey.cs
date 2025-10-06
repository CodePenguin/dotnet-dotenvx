using System;
using NBitcoin.Secp256k1;

namespace dotnet_dotenvx.Logic;

public static class EciesJsPrivateKey
{
    /// <summary>
    /// C# equivalent for encapsulate function from https://github.com/ecies/js/blob/master/src/keys/PrivateKey.ts
    /// </summary>
    public static byte[] Encapsulate(byte[] privateKey, byte[] publicKey, bool compressed = false)
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
        var senderPoint = GetPublicKey(privateKey, compressed);
        var sharedPoint = ecPublicKey.GetSharedPubkey(ecPrivateKey);

        return EciesJsUtils.GetSharedKey(senderPoint, sharedPoint.ToBytes(compressed));
    }
    
    public static byte[] GetPublicKey(byte[] privateKey, bool compressed = false)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        if (privateKey.Length != 32) throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));

        var ctx = Context.Instance;

        // create EC private key object (checks scalar validity)
        if (!ctx.TryCreateECPrivKey(privateKey, out var ecPrivateKey))
        {
            throw new ArgumentException("Invalid private key", nameof(privateKey));
        }

        var publicKey = ecPrivateKey.CreatePubKey();
        return publicKey.ToBytes(compressed);
    }
}