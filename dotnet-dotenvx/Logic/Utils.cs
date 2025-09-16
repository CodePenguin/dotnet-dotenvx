using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace dotnet_dotenvx.Logic;

public static class Utils
{
    public static string AesDecrypt(BigInteger key, byte[] cipherText)
    {
        var nonce = cipherText[..16];
        var tag = cipherText[16..32];
        var ciphered = cipherText[32..];
        
        // Prepare AES-GCM Cipher
        var keyParameters = new KeyParameter(key.ToByteArrayUnsigned());
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(keyParameters, tag.Length * 8, nonce);
        cipher.Init(false, parameters);
        
        // Decrypt the cipher text
        byte[] ciphertextTagBytes = [..ciphered, ..tag];
        var plaintextBytes = new byte[cipher.GetOutputSize(ciphertextTagBytes.Length)];
        var offset = cipher.ProcessBytes(ciphertextTagBytes, 0, ciphertextTagBytes.Length, plaintextBytes, 0);
        cipher.DoFinal(plaintextBytes, offset);
        
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}