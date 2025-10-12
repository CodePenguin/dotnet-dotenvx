namespace Dotenvx.Core.Interop.EciesJs;

/// <summary>
///     C# equivalent for eciesjs src/index.ts (https://github.com/ecies/js).
/// </summary>
public static class EciesJs
{
    private const int PublicKeyUncompressedLength = 65;

    public static byte[] Decrypt(string receiverPrivateKeyHex, byte[] data)
    {
        byte[] receiverPrivateKey = Utils.DecodeHex(receiverPrivateKeyHex);
        if (data.Length < PublicKeyUncompressedLength)
        {
            throw new Exception("second arg must be public key");
        }

        byte[] encodedPublicKey = data[..PublicKeyUncompressedLength];
        byte[] encrypted = data[PublicKeyUncompressedLength..];
        byte[] sharedKey = new PublicKey(encodedPublicKey).Decapsulate(receiverPrivateKey);
        return Utils.AesDecrypt(sharedKey, encrypted);
    }

    public static byte[] Encrypt(string receiverPublicKeyHex, byte[] data)
    {
        PrivateKey privateKey = new();
        byte[] receiverPublicKey = Utils.DecodeHex(receiverPublicKeyHex);
        byte[] sharedKey = privateKey.Encapsulate(receiverPublicKey);
        byte[] publicKey = privateKey.GetPublicKey();
        byte[] encrypted = Utils.AesEncrypt(sharedKey, data);
        return [..publicKey, ..encrypted];
    }
}