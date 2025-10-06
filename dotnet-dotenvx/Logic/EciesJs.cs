namespace dotnet_dotenvx.Logic;

/// <summary>
/// C# equivalent for eciesjs src/index.ts (https://github.com/ecies/js).
/// </summary>
public static class EciesJs
{
    private const int PublicKeyUncompressedLength = 65;
    
    public static byte[] Decrypt(string receiverPrivateKeyHex, byte[] msg)
    {
        var receiverPrivateKey = EciesJsUtils.DecodeHex(receiverPrivateKeyHex);
        var encodedPublicKey = msg[..PublicKeyUncompressedLength];
        var encrypted = msg[PublicKeyUncompressedLength..];
        var aesKey = EciesJsPublicKey.Decapsulate(encodedPublicKey, receiverPrivateKey);
        return EciesJsUtils.AesDecrypt(aesKey, encrypted);
    }

    /*
    public static byte[] Encrypt(string receiverPublicKeyHex, byte[] msg)
    {
        // Decode Public Key
        var secret = EciesJsUtils.DecodeHex(receiverPublicKeyHex);
        var q = Secp256k1Js.Ecp.Curve.DecodePoint(secret);
        var receiverPublicKey = new ECPublicKeyParameters(q, Secp256k1Js.DomainParameters);
        
        // Generate new disposable key pair
        var secureRandom = new SecureRandom();
        var keyParams = new ECKeyGenerationParameters(Secp256k1Js.DomainParameters, secureRandom);
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
    */
}