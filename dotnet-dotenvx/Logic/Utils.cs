using System;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;

namespace dotnet_dotenvx.Logic;

public static class Utils
{
    private const int TagLength = 16;

    public static byte[] AesDecrypt(byte[] key, byte[] data)
    {
        // Split Nonce/Tag/CipherText
        var nonce = data[..16];
        var tag = data[16..32];
        var cipherText = data[32..];
        
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

    public static byte[] AesEncrypt(byte[] key, byte[] data)
    {
        // Generate Nonce
        var nonce = new byte[16];
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
    
    public static byte[] DecodeHex(string value) {
        return Hex.Decode(value.StartsWith("0x") ? value[2..] : value);
    }
}