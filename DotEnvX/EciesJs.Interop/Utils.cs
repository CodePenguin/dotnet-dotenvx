using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;

namespace DotEnvX.EciesJs.Interop;

/// <summary>
///     C# equivalent for eciesjs src/utils.js (https://github.com/ecies/js).
/// </summary>
public static class Utils
{
    private const int NonceLength = 16;
    private const int TagLength = 16;
    private const int TagIndex = NonceLength;
    private const int CipherTextIndex = NonceLength + TagLength;

    /// <summary>
    /// C# equivalent for symDecrypt function from https://github.com/ecies/js/blob/master/src/utils/symmetric.ts
    /// </summary>
    public static byte[] AesDecrypt(byte[] key, byte[] data)
    {
        // Split Nonce/Tag/CipherText
        var nonce = data[..TagIndex];
        var tag = data[TagIndex..CipherTextIndex];
        var cipherText = data[CipherTextIndex..];

        // Prepare AES-GCM Cipher
        var keyParameters = new KeyParameter(key);
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(keyParameters, TagLength * 8, nonce);
        cipher.Init(false, parameters);

        // Decrypt the CipherText/Tag
        byte[] ciphertextTagBytes = [..cipherText, ..tag];
        var decrypted = new byte[cipher.GetOutputSize(ciphertextTagBytes.Length)];
        var processedLength = cipher.ProcessBytes(ciphertextTagBytes, 0, ciphertextTagBytes.Length, decrypted, 0);
        cipher.DoFinal(decrypted, processedLength);

        return decrypted;
    }

    /// <summary>
    /// C# equivalent for symEncrypt function from https://github.com/ecies/js/blob/master/src/utils/symmetric.ts
    /// </summary>
    public static byte[] AesEncrypt(byte[] key, byte[] data)
    {
        // Generate Nonce
        var nonce = new byte[NonceLength];
        var random = new SecureRandom();
        random.NextBytes(nonce);

        // Prepare AES-GCM Cipher
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), TagLength * 8, nonce);
        cipher.Init(true, parameters);

        // Encrypt the data
        var cipherTextTag = new byte[cipher.GetOutputSize(data.Length)];
        var processedLength = cipher.ProcessBytes(data, 0, data.Length, cipherTextTag, 0);
        cipher.DoFinal(cipherTextTag, processedLength);

        // Rearrange in Nonce/Tag/CipherText order
        var cipherTextLength = cipherTextTag.Length - TagLength;
        var nonceTagCipherText = new byte[nonce.Length + cipherTextTag.Length];
        Buffer.BlockCopy(nonce, 0, nonceTagCipherText, 0, nonce.Length);
        Buffer.BlockCopy(cipherTextTag, cipherTextLength, nonceTagCipherText, nonce.Length, TagLength);
        Buffer.BlockCopy(cipherTextTag, 0, nonceTagCipherText, nonce.Length + TagLength, cipherTextLength);
        return nonceTagCipherText;
    }

    /// <summary>
    /// C# equivalent for decodeHex function from https://github.com/ecies/js/blob/master/src/utils/hex.ts
    /// </summary>
    public static byte[] DecodeHex(string value)
    {
        return Convert.FromHexString(value.StartsWith("0x") ? value[2..] : value);
    }

    /// <summary>
    /// C# equivalent for getSharedKey function from https://github.com/ecies/js/blob/master/src/utils/hash.ts
    /// </summary>
    public static byte[] GetSharedKey(byte[] publicKeyBytes, byte[] sharedPointBytes)
    {
        byte[] master = [..publicKeyBytes, ..sharedPointBytes];
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32);
    }
}