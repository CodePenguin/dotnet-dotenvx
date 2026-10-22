using Org.BouncyCastle.Math.EC;

namespace Dotenvx.Primitives.Interop.EciesJs;

/// <summary>
///     C# equivalent for eciesjs src/keys/PublicKey.ts (https://github.com/ecies/js).
/// </summary>
internal class PublicKey
{
    private readonly ECPoint _publicKey;

    public PublicKey(byte[] publicKey)
    {
        if (!Secp256k1.TryDecodePoint(publicKey, out ECPoint point))
        {
            throw new ArgumentException("Invalid public key", nameof(publicKey));
        }

        _publicKey = point;
    }

    public byte[] Decapsulate(byte[] privateKey, bool compressed = false)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));
        }

        if (!Secp256k1.TryParsePrivateKey(privateKey, out var scalar))
        {
            throw new ArgumentException("Invalid private key", nameof(privateKey));
        }

        // Get Shared Public Key
        ECPoint sharedPoint = Secp256k1.Multiply(_publicKey, scalar);

        return Utils.GetSharedKey(Secp256k1.Encode(_publicKey, compressed), Secp256k1.Encode(sharedPoint, compressed));
    }

    public static PublicKey FromHex(string hex)
    {
        return new PublicKey(Utils.DecodeHex(hex));
    }

    public byte[] ToBytes(bool compressed = true)
    {
        return Secp256k1.Encode(_publicKey, compressed);
    }

    public string ToHex(bool compressed = true)
    {
        return Convert.ToHexString(ToBytes(compressed)).ToLower();
    }
}
