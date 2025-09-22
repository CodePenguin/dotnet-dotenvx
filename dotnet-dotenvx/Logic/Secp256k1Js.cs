using System;
using NBitcoin.Secp256k1;

namespace dotnet_dotenvx.Logic;

public static class Secp256k1Js
{
    /// <summary>
    /// C# equivalent for secp256k1 v3.6.0 lib/elliptic/index.js (https://github.com/cryptocoinjs/secp256k1-node).
    /// </summary>
    public static byte[] Ecdh(byte[] publicKey, byte[] privateKey)
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

        // Multiply: sharedPoint = pubKey * privKey
        var sharedPoint = ecPublicKey.GetSharedPubkey(ecPrivateKey);

        // Encode as compressed (33 bytes)
        var compressedSharedPoint = sharedPoint.ToBytes(true);
        using var sha = new SHA256();
        sha.Write(new ReadOnlySpan<byte>(compressedSharedPoint));
        return sha.GetHash();
    }

    public static byte[] GetPublicKey(byte[] privateKey, bool compressed = true)
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