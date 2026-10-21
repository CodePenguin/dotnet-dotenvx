using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Dotenvx.Primitives.Interop.EciesJs;

/// <summary>
///     secp256k1 operations on Bouncy Castle, covering what eciesjs takes from @noble/curves: key validation, public key
///     derivation, point decoding and scalar multiplication.
/// </summary>
internal static class Secp256k1
{
    private static readonly X9ECParameters Curve = CustomNamedCurves.GetByName("secp256k1");
    private static readonly ECDomainParameters Domain = new(Curve.Curve, Curve.G, Curve.N, Curve.H);

    /// <summary>A private key is a scalar d with 1 &lt;= d &lt; n.</summary>
    public static bool TryParsePrivateKey(byte[] privateKey, out BigInteger scalar)
    {
        scalar = new BigInteger(1, privateKey);

        return privateKey.Length == 32 && scalar.SignValue > 0 && scalar.CompareTo(Domain.N) < 0;
    }

    /// <summary>Accepts only 33 byte compressed (02/03) and 65 byte uncompressed (04) points that lie on the curve.</summary>
    public static bool TryDecodePoint(byte[] publicKey, out ECPoint point)
    {
        point = Domain.Curve.Infinity;
        bool shape = (publicKey.Length == 33 && publicKey[0] is 0x02 or 0x03) || (publicKey.Length == 65 && publicKey[0] == 0x04);
        if (!shape)
        {
            return false;
        }

        try
        {
            point = Domain.Curve.DecodePoint(publicKey);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return !point.IsInfinity && point.IsValid();
    }

    public static ECPoint Multiply(ECPoint point, BigInteger scalar) => point.Multiply(scalar).Normalize();

    public static ECPoint CreatePublicKey(BigInteger scalar) => Multiply(Domain.G, scalar);

    public static byte[] Encode(ECPoint point, bool compressed) => point.GetEncoded(compressed);
}
