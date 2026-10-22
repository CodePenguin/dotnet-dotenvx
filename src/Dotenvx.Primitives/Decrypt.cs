using System.Text;


using Org.BouncyCastle.Crypto;

namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `decrypt(privateKeyHex, value, { prefix })` export (checked
/// live): returns the value untouched when it is not encrypted, throws MISSING_PRIVATE_KEY before
/// touching the ciphertext, then maps failures to INVALID_PRIVATE_KEY, WRONG_PRIVATE_KEY,
/// MALFORMED_ENCRYPTED_DATA or DECRYPTION_FAILED. These errors carry no key name (unlike dotenvx's own
/// wrapper, Core's DecryptKeyValue).
/// </remarks>
public static partial class Primitives
{
    private const string FixUrl = "fix: [https://github.com/dotenvx/dotenvx/issues/";

    public static string Decrypt(string? privateKeyHex, string value, bool prefix = true)
    {
        string r = prefix ? value : Prefix + value;
        if (!Encrypted(r))
        {
            return value;
        }

        if (string.IsNullOrEmpty(privateKeyHex))
        {
            throw new DotenvxException("MISSING_PRIVATE_KEY",
                "[MISSING_PRIVATE_KEY] could not decrypt because private key is missing", FixUrl + "464]");
        }

        try
        {
            byte[] cipher;
            try
            {
                cipher = Convert.FromBase64String(r[Prefix.Length..]);
            }
            catch (FormatException)
            {
                throw new DotenvxException("DECRYPTION_FAILED", "[DECRYPTION_FAILED] second arg must be public key",
                    FixUrl + "757]");
            }

            if (cipher.Length == 0)
            {
                throw new DotenvxException("DECRYPTION_FAILED", "[DECRYPTION_FAILED] second arg must be public key",
                    FixUrl + "757]");
            }

            return Encoding.UTF8.GetString(Interop.EciesJs.EciesJs.Decrypt(privateKeyHex, cipher));
        }
        catch (DotenvxException)
        {
            throw;
        }
        catch (ArgumentException ex) when (ex.ParamName == "privateKey")
        {
            throw InvalidKey();
        }
        catch (FormatException)
        {
            throw InvalidKey();
        }
        catch (ArgumentException)
        {
            throw new DotenvxException("MALFORMED_ENCRYPTED_DATA",
                "[MALFORMED_ENCRYPTED_DATA] could not decrypt because encrypted data appears malformed",
                FixUrl + "467]");
        }
        catch (InvalidCipherTextException)
        {
            throw new DotenvxException("WRONG_PRIVATE_KEY", "[WRONG_PRIVATE_KEY] could not decrypt using private key",
                FixUrl + "466]");
        }
        catch (Exception ex)
        {
            throw new DotenvxException("DECRYPTION_FAILED", "[DECRYPTION_FAILED] " + ex.Message, FixUrl + "757]");
        }
    }

    private static DotenvxException InvalidKey()
    {
        return new DotenvxException("INVALID_PRIVATE_KEY",
            "[INVALID_PRIVATE_KEY] could not decrypt using private key", FixUrl + "465]");
    }
}
