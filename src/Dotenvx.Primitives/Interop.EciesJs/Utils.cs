using System.Security.Cryptography;
using System.Text.RegularExpressions;

using NBitcoin.Secp256k1;

using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Dotenvx.Primitives.Interop.EciesJs;

/// <summary>
///     C# equivalent for eciesjs src/utils.js (https://github.com/ecies/js).
/// </summary>
public static partial class Utils
{
    private const int NonceLength = 16;
    private const int TagLength = 16;
    private const int TagIndex = NonceLength;
    private const int CipherTextIndex = NonceLength + TagLength;

    /// <summary>
    ///     C# equivalent for symDecrypt function from https://github.com/ecies/js/blob/master/src/utils/symmetric.ts
    /// </summary>
    public static byte[] AesDecrypt(byte[] key, byte[] data)
    {
        // Split Nonce/Tag/CipherText
        byte[] nonce = data[..TagIndex];
        byte[] tag = data[TagIndex..CipherTextIndex];
        byte[] cipherText = data[CipherTextIndex..];

        // Prepare AES-GCM Cipher
        KeyParameter keyParameters = new(key);
        GcmBlockCipher cipher = new(new AesEngine());
        AeadParameters parameters = new(keyParameters, TagLength * 8, nonce);
        cipher.Init(false, parameters);

        // Decrypt the CipherText/Tag
        byte[] ciphertextTagBytes = [..cipherText, ..tag];
        byte[] decrypted = new byte[cipher.GetOutputSize(ciphertextTagBytes.Length)];
        int processedLength = cipher.ProcessBytes(ciphertextTagBytes, 0, ciphertextTagBytes.Length, decrypted, 0);
        cipher.DoFinal(decrypted, processedLength);

        return decrypted;
    }

    /// <summary>
    ///     C# equivalent for symEncrypt function from https://github.com/ecies/js/blob/master/src/utils/symmetric.ts
    /// </summary>
    public static byte[] AesEncrypt(byte[] key, byte[] data)
    {
        // Generate Nonce
        byte[] nonce = new byte[NonceLength];
        SecureRandom random = new();
        random.NextBytes(nonce);

        // Prepare AES-GCM Cipher
        GcmBlockCipher cipher = new(new AesEngine());
        AeadParameters parameters = new(new KeyParameter(key), TagLength * 8, nonce);
        cipher.Init(true, parameters);

        // Encrypt the data
        byte[] cipherTextTag = new byte[cipher.GetOutputSize(data.Length)];
        int processedLength = cipher.ProcessBytes(data, 0, data.Length, cipherTextTag, 0);
        cipher.DoFinal(cipherTextTag, processedLength);

        // Rearrange in Nonce/Tag/CipherText order
        int cipherTextLength = cipherTextTag.Length - TagLength;
        byte[] nonceTagCipherText = new byte[nonce.Length + cipherTextTag.Length];
        Buffer.BlockCopy(nonce, 0, nonceTagCipherText, 0, nonce.Length);
        Buffer.BlockCopy(cipherTextTag, cipherTextLength, nonceTagCipherText, nonce.Length, TagLength);
        Buffer.BlockCopy(cipherTextTag, 0, nonceTagCipherText, nonce.Length + TagLength, cipherTextLength);
        return nonceTagCipherText;
    }

    /// <summary>
    ///     C# equivalent for decodeHex function from https://github.com/ecies/js/blob/master/src/utils/hex.ts
    /// </summary>
    public static byte[] DecodeHex(string value)
    {
        // Remove "0x" prefix if present
        value = value.StartsWith("0x") ? value[2..] : value;
        // Node.js's Buffer.from(value, 'hex') truncates at the first non-hex character
        value = ExtractFirstHexString().Match(value).Value;
        return Convert.FromHexString(value);
    }

    /// <summary>
    ///     C# equivalent for getPublicKey function from https://github.com/ecies/js/blob/master/src/utils/elliptic.ts
    /// </summary>
    public static byte[] GetPublicKey(byte[] privateKey, bool compressed = false)
    {
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));
        }

        if (!Context.Instance.TryCreateECPrivKey(privateKey, out ECPrivKey? ecPrivateKey))
        {
            throw new ArgumentException("Invalid private key", nameof(privateKey));
        }

        ECPubKey publicKey = ecPrivateKey.CreatePubKey();
        return publicKey.ToBytes(compressed);
    }

    /// <summary>
    ///     C# equivalent for getSharedKey function from https://github.com/ecies/js/blob/master/src/utils/hash.ts
    /// </summary>
    public static byte[] GetSharedKey(byte[] publicKeyBytes, byte[] sharedPointBytes)
    {
        byte[] master = [..publicKeyBytes, ..sharedPointBytes];
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32);
    }

    [GeneratedRegex("^[0-9a-fA-F]*")]
    private static partial Regex ExtractFirstHexString();
}