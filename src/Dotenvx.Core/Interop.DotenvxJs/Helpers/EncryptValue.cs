using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/encryptValue.js</remarks>
public static partial class Helpers
{
    private const string Prefix = "encrypted:";

    public static string EncryptValue(string value, string publicKey)
    {
        byte[] ciphertext = EciesJs.EciesJs.Encrypt(publicKey, Encoding.UTF8.GetBytes(value));
        string encoded = Convert.ToBase64String(ciphertext);
        return $"{Prefix}{encoded}";
    }
}