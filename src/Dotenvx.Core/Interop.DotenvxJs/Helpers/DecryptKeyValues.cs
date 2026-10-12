using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/cryptography/decryptKeyValue.js, a thin wrapper around
/// @dotenvx/primitives' `decrypt()` export (the actual decryption is `Primitives.Decrypt`). It checks for a
/// missing private key first, tries each comma-separated key in turn, and turns primitives' code-only errors
/// into the key-aware INVALID_PRIVATE_KEY / WRONG_PRIVATE_KEY / MALFORMED_ENCRYPTED_DATA / DECRYPTION_FAILED
/// errors.
/// </remarks>
public static partial class Helpers
{
    public static string DecryptKeyValue(string key, string value, string privateKeyName, string? privateKey)
    {
        if (!value.StartsWith(Prefix))
        {
            return value;
        }

        privateKey ??= "";
        string decryptedValue = "";
        Exception? decryptionError = null;

        if (privateKey.Length <= 0)
        {
            decryptionError = new MissingPrivateKeyException(key, privateKeyName, privateKey);
        }
        else
        {
            foreach (string privKey in privateKey.Split(','))
            {
                try
                {
                    decryptedValue = Decrypt(privKey, value);
                    decryptionError = null; // reset to null error (scenario for multiple private keys)
                    break;
                }
                catch (DotenvxException e)
                {
                    decryptionError = e.Code switch
                    {
                        "INVALID_PRIVATE_KEY" => new InvalidPrivateKeyException(key, privateKeyName, privateKey),
                        "WRONG_PRIVATE_KEY" => new WrongPrivateKeyException(key, privateKeyName, privateKey),
                        "MALFORMED_ENCRYPTED_DATA" => new MalformedEncryptedDataException(key),
                        _ => new DecryptionFailedException(PrimitiveMessage(e), key)
                    };
                }
            }
        }

        return decryptionError != null ? throw decryptionError : decryptedValue;
    }

    private static string PrimitiveMessage(Exception error)
    {
        return PrimitiveCodePrefix().Replace(error.Message, "");
    }

    [GeneratedRegex(@"^\[[A-Z_]+\] ")]
    private static partial Regex PrimitiveCodePrefix();
}
