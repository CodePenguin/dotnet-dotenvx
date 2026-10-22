using System.Security.Cryptography;

using Org.BouncyCastle.Math;

namespace Dotenvx.Primitives.Interop.EciesJs;

/// <summary>
///     C# equivalent for eciesjs src/keys/PrivateKey.ts (https://github.com/ecies/js).
/// </summary>
internal class PrivateKey
{
    private readonly byte[] _bytes;
    private readonly BigInteger _scalar;

    public PrivateKey()
    {
        byte[] privateKey;
        do
        {
            privateKey = RandomNumberGenerator.GetBytes(32);
        }
        while (!Secp256k1.TryParsePrivateKey(privateKey, out _));

        (_bytes, _scalar) = LoadFromBytes(privateKey);
        PublicKey = new PublicKey(Utils.GetPublicKey(privateKey));
    }

    public PrivateKey(byte[] privateKey)
    {
        (_bytes, _scalar) = LoadFromBytes(privateKey);
        PublicKey = new PublicKey(Secp256k1.Encode(Secp256k1.CreatePublicKey(_scalar), true));
    }

    public PublicKey PublicKey { get; }

    public byte[] Encapsulate(byte[] publicKey, bool compressed = false)
    {
        if (!Secp256k1.TryDecodePoint(publicKey, out var point))
        {
            throw new ArgumentException("Invalid public key", nameof(publicKey));
        }

        byte[] senderPoint = PublicKey.ToBytes(compressed);
        byte[] sharedPoint = Secp256k1.Encode(Secp256k1.Multiply(point, _scalar), compressed);

        return Utils.GetSharedKey(senderPoint, sharedPoint);
    }

    public static PrivateKey FromHex(string hex)
    {
        return new PrivateKey(Utils.DecodeHex(hex));
    }

    private static (byte[] Bytes, BigInteger Scalar) LoadFromBytes(byte[] privateKey)
    {
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));
        }

        return Secp256k1.TryParsePrivateKey(privateKey, out BigInteger scalar)
            ? ((byte[])privateKey.Clone(), scalar)
            : throw new ArgumentException("Invalid private key", nameof(privateKey));
    }

    public string ToHex()
    {
        return Convert.ToHexString(_bytes).ToLower();
    }
}
