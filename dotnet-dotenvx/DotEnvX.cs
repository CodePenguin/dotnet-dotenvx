using System;
using System.Text;
using dotnet_dotenvx.Extensions;
using Org.BouncyCastle.Crypto;

namespace dotnet_dotenvx;

public static class DotEnvX
{
    private const string Prefix = "encrypted:";

    public static string? DecryptKeyValue(string key, string value, string privateKeyName, string? privateKey)
    {
        if (!value.StartsWith(Prefix)) return value;
        
        var encoded = value[Prefix.Length..];
        var cipherText = Convert.FromBase64String(encoded);
        
        if (string.IsNullOrEmpty(encoded))
        {
            throw new DotEnvXException("DECRYPTION_FAILED", "second arg must be public key");
        }
        
        if (string.IsNullOrEmpty(privateKey))
        {
            throw new DotEnvXException("MISSING_PRIVATE_KEY", $"[MISSING_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={privateKey.Truncate()}'");
        }

        string? decryptedValue = null;
        Exception? decryptionError = null;
        
        var privateKeys = privateKey.Split(',');
        foreach (var privateKeyHex in privateKeys)
        {
            try
            {
                var decryptedBytes = EciesJs.EciesJs.Decrypt(privateKeyHex, cipherText);
                decryptedValue = Encoding.UTF8.GetString(decryptedBytes);
                decryptionError = null;
                break;
            }
            catch (ArgumentException ex)
            {
                decryptionError = ex.ParamName switch
                {
                    nameof(privateKeyName) => new DotEnvXException("INVALID_PRIVATE_KEY", $"[INVALID_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={privateKey.Truncate()}'"),
                    _ => new DotEnvXException("MALFORMED_ENCRYPTED_DATA", $"[MALFORMED_ENCRYPTED_DATA] could not decrypt {key} because encrypted data appears malformed")
                };
            }
            catch (FormatException)
            {
                decryptionError = new DotEnvXException("INVALID_PRIVATE_KEY", $"[INVALID_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={privateKey.Truncate()}'");
            }
            catch (InvalidCipherTextException)
            {
                decryptionError = new DotEnvXException("WRONG_PRIVATE_KEY", $"[WRONG_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={privateKey.Truncate()}'");
            }
            catch (Exception ex)
            {
                decryptionError = new DotEnvXException("DECRYPTION_FAILED", ex.Message);
            }
        }

        return decryptionError != null ? throw decryptionError : decryptedValue;
    }

    public static string EncryptValue(string value, string publicKey)
    {
        var ciphertext = EciesJs.EciesJs.Encrypt(publicKey, Encoding.UTF8.GetBytes(value));
        var encoded = Convert.ToBase64String(ciphertext);
        return $"{Prefix}{encoded}";
    }
}