using System.Text;

using Dotenvx.Interop.DotenvxJs.Types;

using Org.BouncyCastle.Crypto;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/cryptography/decryptKeyValue.js, a thin wrapper around
/// @dotenvx/primitives' `decrypt()` export that translates its generic failure into one of
/// missingPrivateKey()/invalidPrivateKey()/wrongPrivateKey()/malformedEncryptedData()/
/// decryptionFailed(). Confirmed via live oracle that the real check order is privateKey-missing
/// FIRST, before ever touching the ciphertext bytes - this port previously decoded the base64
/// ciphertext unconditionally before checking for a missing private key, which both reordered the
/// resulting error for an empty-ciphertext-with-no-private-key input and let a malformed (non-
/// base64) ciphertext throw a raw, uncoded .NET FormatException straight to the user instead of a
/// proper DotenvxException - .NET's Convert.FromBase64String is strict, unlike Node's lenient
/// Buffer.from(..., 'base64'), so this divergence is real even though upstream's own ordering logic
/// doesn't need to guard against a decode exception at all.
/// </remarks>
public static partial class Helpers
{
    public static string DecryptKeyValue(string key, string value, string privateKeyName, string? privateKey)
    {
        if (!value.StartsWith(Prefix))
        {
            return value;
        }

        string encoded = value[Prefix.Length..];

        if (string.IsNullOrEmpty(encoded))
        {
            throw new DecryptionFailedException("second arg must be public key", key);
        }

        if (string.IsNullOrEmpty(privateKey))
        {
            throw new MissingPrivateKeyException(key, privateKeyName, privateKey ?? string.Empty);
        }

        byte[] cipherText;
        try
        {
            cipherText = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new DecryptionFailedException("second arg must be public key", key);
        }

        string decryptedValue = string.Empty;
        Exception? decryptionError = null;

        string[] privateKeys = privateKey.Split(',');
        foreach (string privateKeyHex in privateKeys)
        {
            try
            {
                byte[] decryptedBytes =
                    global::Dotenvx.Primitives.Interop.EciesJs.EciesJs.Decrypt(privateKeyHex, cipherText);
                decryptedValue = Encoding.UTF8.GetString(decryptedBytes);
                decryptionError = null;
                break;
            }
            catch (ArgumentException ex)
            {
                decryptionError = ex.ParamName switch
                {
                    nameof(privateKey) => new InvalidPrivateKeyException(key, privateKeyName, privateKey),
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
                decryptionError = new DecryptionFailedException(ex.Message, key);
            }
        }

        return decryptionError != null ? throw decryptionError : decryptedValue;
    }
}