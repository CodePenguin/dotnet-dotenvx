using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace dotnet_dotenvx.Logic;

// C# equivalent for https://github.com/eusend/eciesjs

public class Ecies
{
    public static byte[] Decrypt(byte[] secret, byte[] msg)
    {
        var ecp = SecNamedCurves.GetByName("secp256k1");
        var domainParameters = new ECDomainParameters(ecp.Curve, ecp.G, ecp.N, ecp.H, ecp.GetSeed());
        
        // Decode Public Key
        var q = ecp.Curve.DecodePoint(msg[..65]);
        var publicKey = new ECPublicKeyParameters(q, domainParameters);
        var encrypted = msg[65..];
        
        // Decode Private Key
        var d = new BigInteger(1, secret);
        var privateKey = new ECPrivateKeyParameters(d, domainParameters);
        
        // Generate AES Shared Secret Key
        var agreement = new ECDHBasicAgreement();
        agreement.Init(privateKey);
        var aesKey = agreement.CalculateAgreement(publicKey);

        return Utils.AesDecrypt(aesKey.ToByteArrayUnsigned(), msg);
    }
}