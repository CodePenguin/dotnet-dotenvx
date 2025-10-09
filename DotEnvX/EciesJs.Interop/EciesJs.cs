namespace DotEnvX.EciesJs.Interop;

/// <summary>
///     C# equivalent for eciesjs src/index.ts (https://github.com/ecies/js).
/// </summary>
public static class EciesJs
{
    private const int PublicKeyUncompressedLength = 65;

    public static byte[] Decrypt(string receiverPrivateKeyHex, byte[] data)
    {
        var receiverPrivateKey = Utils.DecodeHex(receiverPrivateKeyHex);
        if (data.Length < PublicKeyUncompressedLength) throw new Exception("second arg must be public key");
        var encodedPublicKey = data[..PublicKeyUncompressedLength];
        var encrypted = data[PublicKeyUncompressedLength..];
        var sharedKey = new PublicKey(encodedPublicKey).Decapsulate(receiverPrivateKey);
        return Utils.AesDecrypt(sharedKey, encrypted);
    }

    public static byte[] Encrypt(string receiverPublicKeyHex, byte[] data)
    {
        var privateKey = new PrivateKey();
        var receiverPublicKey = Utils.DecodeHex(receiverPublicKeyHex);
        var sharedKey = privateKey.Encapsulate(receiverPublicKey);
        var publicKey = privateKey.GetPublicKey();
        var encrypted = Utils.AesEncrypt(sharedKey, data);
        return [..publicKey, ..encrypted];
    }
}