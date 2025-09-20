using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace dotnet_dotenvx.Logic;

/// <summary>
/// C# equivalent for eciesjs src/index.ts (https://github.com/eusend/eciesjs).
/// </summary>
public static class EciesJs
{
    private const int PublicKeyUncompressedLength = 65;

    private static readonly X9ECParameters Ecp = SecNamedCurves.GetByName("secp256k1");
    private static readonly ECDomainParameters DomainParameters = new(Ecp.Curve, Ecp.G, Ecp.N, Ecp.H, Ecp.GetSeed());
    public static byte[] Decrypt(string receiverPrivateKeyHex, byte[] msg)
    {
        // Decode Public Key
        var encodedPublicKey = msg[..PublicKeyUncompressedLength];
        var q = Ecp.Curve.DecodePoint(encodedPublicKey);
        var publicKey = new ECPublicKeyParameters(q, DomainParameters);
        var encrypted = msg[PublicKeyUncompressedLength..];
        
        // Decode Private Key
        var secret = EciesJsUtils.DecodeHex(receiverPrivateKeyHex);
        var d = new BigInteger(1, secret);
        var privateKey = new ECPrivateKeyParameters(d, DomainParameters);
        
        // Generate AES Shared Secret Key
        var agreement = new ECDHBasicAgreement();
        agreement.Init(privateKey);
        var aesKey = agreement.CalculateAgreement(publicKey);

        return EciesJsUtils.AesDecrypt(aesKey.ToByteArrayUnsigned(), encrypted);
    }

    public static byte[] Encrypt(string receiverPublicKeyHex, byte[] msg)
    {
        // Decode Public Key
        var secret = EciesJsUtils.DecodeHex(receiverPublicKeyHex);
        var q = Ecp.Curve.DecodePoint(secret);
        var receiverPublicKey = new ECPublicKeyParameters(q, DomainParameters);
        
        // Generate new disposable key pair
        var secureRandom = new SecureRandom();
        var keyParams = new ECKeyGenerationParameters(DomainParameters, secureRandom);
        var generator = new ECKeyPairGenerator("ECDSA");
        generator.Init(keyParams);
        var disposableKeyPair = generator.GenerateKeyPair();
        var encodedPublicKey = ((ECPublicKeyParameters)disposableKeyPair.Public).Q.GetEncoded();
        
        // Generate AES Shared Secret Key
        var agreement = new ECDHBasicAgreement();
        agreement.Init(disposableKeyPair.Private);
        var aesKey = agreement.CalculateAgreement(receiverPublicKey);

        // Encrypt the data and return as PublicKey + Encrypted
        var encrypted = EciesJsUtils.AesEncrypt(aesKey.ToByteArrayUnsigned(), msg);
        return [..encodedPublicKey, ..encrypted];
    }
}