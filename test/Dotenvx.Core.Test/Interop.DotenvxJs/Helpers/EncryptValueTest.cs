using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/encrypt-value.test.js</remarks>
public class EncryptValueTest
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    /// <remarks>Refer to "#encryptValue"</remarks>
    [Fact]
    public void TestEncryptValue()
    {
        string result = EncryptValue("hello", PublicKey);
        result.ShouldStartWith("encrypted:");

        string? decrypted = DecryptKeyValue("KEY", result, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("hello");
    }

    /// <remarks>Refer to "#encryptValue with implicit newlines"</remarks>
    [Fact]
    public void TestEncryptValueImplicitNewlines()
    {
        const string value = @"line 1
line 2
line 3";

        string result = EncryptValue(value, PublicKey);
        result.ShouldStartWith("encrypted:");

        string? decrypted = DecryptKeyValue("KEY", result, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe(value);
    }

    /// <remarks>Refer to "#encryptValue with explicit newlines"</remarks>
    [Fact]
    public void TestEncryptValueExplicitNewlines()
    {
        const string value = "line 1\nline 2\nline 3";

        string result = EncryptValue(value, PublicKey);
        result.ShouldStartWith("encrypted:");

        string? decrypted = DecryptKeyValue("KEY", result, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe(value);
    }
}