using System.Text;

using Dotenvx.Core.Interop.Dotenvx.Types;

using Org.BouncyCastle.Crypto;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static partial class Helpers
{
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
}