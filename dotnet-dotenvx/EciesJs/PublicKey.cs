using System;
using NBitcoin.Secp256k1;

namespace dotnet_dotenvx.EciesJs;

public class PublicKey
{
    private readonly Context _context = Context.Instance;
    private readonly ECPubKey _publicKey;

    public PublicKey(byte[] publicKey)
    {
        if (!ECPubKey.TryCreate(publicKey, _context, out _, out var ecPublicKey)) throw new ArgumentException("Invalid public key", nameof(publicKey));
        _publicKey = ecPublicKey;
    }

    /// <summary>
    ///     C# equivalent for decapsulate function from https://github.com/ecies/js/blob/master/src/keys/PublicKey.ts
    /// </summary>
    public byte[] Decapsulate(byte[] privateKey, bool compressed = false)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        if (privateKey.Length != 32) throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));

        if (!_context.TryCreateECPrivKey(privateKey, out var ecPrivateKey)) throw new ArgumentException("Invalid private key", nameof(privateKey));

        // Get Shared Public Key
        var sharedPoint = _publicKey.GetSharedPubkey(ecPrivateKey);

        return Utils.GetSharedKey(_publicKey.ToBytes(compressed), sharedPoint.ToBytes(compressed));
    }

    public static PublicKey FromHex(string hex)
    {
        return new PublicKey(Utils.DecodeHex(hex));
    }
}