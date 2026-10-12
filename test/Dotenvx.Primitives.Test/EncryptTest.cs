using System.Text;

using Dotenvx.Primitives.Interop.EciesJs;

using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/encrypt-value.test.js</remarks>
public class EncryptTest
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    // Decrypts a raw `Encrypt()` result without going through Dotenvx.Core's DecryptKeyValue
    // wrapper (the dotenvx-own error-translation layer, not part of this project) - this project
    // has no dependency on Core, so round-trip verification here uses the low-level primitive
    // directly.
    private static string DecryptForTest(string encryptedValue)
    {
        byte[] cipherText = Convert.FromBase64String(encryptedValue[Prefix.Length..]);
        byte[] plainBytes = EciesJs.Decrypt(PrivateKey, cipherText);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <remarks>Refer to "#encryptValue"</remarks>
    [Fact]
    public void TestEncrypt()
    {
        string result = Encrypt("hello", PublicKey);
        result.ShouldStartWith("encrypted:");
        DecryptForTest(result).ShouldBe("hello");
    }

    /// <remarks>Refer to "#encryptValue with implicit newlines"</remarks>
    [Fact]
    public void TestEncryptValueImplicitNewlines()
    {
        const string value = @"line 1
line 2
line 3";

        string result = Encrypt(value, PublicKey);
        result.ShouldStartWith("encrypted:");
        DecryptForTest(result).ShouldBe(value);
    }

    /// <remarks>Refer to "#encryptValue with explicit newlines"</remarks>
    [Fact]
    public void TestEncryptValueExplicitNewlines()
    {
        const string value = "line 1\nline 2\nline 3";

        string result = Encrypt(value, PublicKey);
        result.ShouldStartWith("encrypted:");
        DecryptForTest(result).ShouldBe(value);
    }
}
