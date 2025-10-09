using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace dotnet_dotenvx.Configuration;

public class DotEnvXConfigurationProvider : ConfigurationProvider
{
    private const string Prefix = "encrypted:";

    private static string? DecryptKeyValue(string value, string privateKey)
    {
        if (!value.StartsWith(Prefix)) return value;

        if (string.IsNullOrEmpty(privateKey)) throw new Exception("Missing private key.");

        string? decryptedValue = null;
        Exception? error = null;

        var privateKeys = privateKey.Split(',');
        foreach (var privateKeyHex in privateKeys)
        {
            var encoded = value[Prefix.Length..];
            var cipherText = Convert.FromBase64String(encoded);
            try
            {
                var decryptedBytes = EciesJs.Interop.EciesJs.Decrypt(privateKeyHex, cipherText);
                decryptedValue = Encoding.UTF8.GetString(decryptedBytes);
                error = null;
                break;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        }

        return error != null ? throw error : decryptedValue;
    }

    public override void Load()
    {
        var variables = new Dictionary<string, string>();

        var privateKey = "bf765d17bd08f4794f4bedc0bea3d5ba9682c7f38f887306f10cb30f870c8900";

        Console.WriteLine($"Private Length: {privateKey.Length}");

        //using var secp256k1 = new Secp256k1();

        var encrypted = "encrypted:BGUxwqrxyfljxy8x+R/Jx9XWKYy2+AbaTu6W3r5JYLb8MFMziwIJycDK+oJVQEMlRLWdAFBrLFZd6lhaewWeKcYnbJ1tFnv1Rq8iOz8sDdAG19fAVF5Z1lvL9UNMjlMx8tLZtFcd1JrfPWBcUNd/+Sx6ygXAyik=";
        var decrypted = DecryptKeyValue(encrypted, privateKey);

        variables.Add("HELLO", decrypted ?? "Bad");

        Data = variables!;
    }
}