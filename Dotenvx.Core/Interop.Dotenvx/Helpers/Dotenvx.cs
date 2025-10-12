using System.Collections;
using System.Text;

using Dotenvx.Core.Interop.Dotenvx.Types;

using Org.BouncyCastle.Crypto;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static class Dotenvx
{
    private const string Prefix = "encrypted:";

    public static ConfigResult Config(ConfigOptions options)
    {
        throw new NotImplementedException();
    }
    
    public static string? DecryptKeyValue(string key, string value, string privateKeyName, string? privateKey)
    {
        if (!value.StartsWith(Prefix))
        {
            return value;
        }

        string encoded = value[Prefix.Length..];
        byte[] cipherText = Convert.FromBase64String(encoded);

        if (string.IsNullOrEmpty(encoded))
        {
            throw new DecryptionFailedException("second arg must be public key");
        }

        if (string.IsNullOrEmpty(privateKey))
        {
            throw new MissingPrivateKeyException(key, privateKeyName, privateKey ?? string.Empty);
        }

        string? decryptedValue = null;
        Exception? decryptionError = null;

        string[] privateKeys = privateKey.Split(',');
        foreach (string privateKeyHex in privateKeys)
        {
            try
            {
                byte[] decryptedBytes = EciesJs.EciesJs.Decrypt(privateKeyHex, cipherText);
                decryptedValue = Encoding.UTF8.GetString(decryptedBytes);
                decryptionError = null;
                break;
            }
            catch (ArgumentException ex)
            {
                decryptionError = ex.ParamName switch
                {
                    nameof(privateKeyName) => new InvalidPrivateKeyException(key, privateKeyName, privateKey),
                    _ => new MalformedEncryptedDataException(key)
                };
            }
            catch (FormatException)
            {
                decryptionError = new InvalidPrivateKeyException(key, privateKeyName, privateKey);
            }
            catch (InvalidCipherTextException)
            {
                decryptionError = new WrongPrivateKeyException(key, privateKeyName, privateKey);
            }
            catch (Exception ex)
            {
                decryptionError = new DecryptionFailedException(ex.Message);
            }
        }

        return decryptionError != null ? throw decryptionError : decryptedValue;
    }

    public static string EncryptValue(string value, string publicKey)
    {
        byte[] ciphertext = EciesJs.EciesJs.Encrypt(publicKey, Encoding.UTF8.GetBytes(value));
        string encoded = Convert.ToBase64String(ciphertext);
        return $"{Prefix}{encoded}";
    }

    public static IDictionary<string, string> Parse(string? src, ParseOptions? options = null)
    {
        IDictionary<string,string> processEnv = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? string.Empty);

        if (options?.ProcessEnv != null)
        {
            processEnv = options.ProcessEnv;
        }

        // private decryption key
        var privateKey = options?.PrivateKey ?? null;

        // overload
        var overload = options?.Overload ?? options?.Override ?? false;
        
        var response = new Parse(src, privateKey, processEnv ?? new Dictionary<string, string>(), overload).Execute();
        
        // display any errors
        // TODO: Need to log errors

        return response.Parsed;
    }
}