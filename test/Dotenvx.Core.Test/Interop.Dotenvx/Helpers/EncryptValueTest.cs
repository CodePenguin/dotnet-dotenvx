using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

public class EncryptValueTest
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    [Fact]
    public void EncryptValue()
    {
        string result = DotenvxHelpers.EncryptValue("hello", PublicKey);
        result.ShouldStartWith("encrypted:");

        string? decrypted = DotenvxHelpers.DecryptKeyValue("KEY", result, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("hello");
    }

    [Fact]
    public void EncryptValue_ImplicitNewlines()
    {
        const string value = @"line 1
line 2
line 3";

        string result = DotenvxHelpers.EncryptValue(value, PublicKey);
        result.ShouldStartWith("encrypted:");

        string? decrypted = DotenvxHelpers.DecryptKeyValue("KEY", result, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe(value);
    }

    [Fact]
    public void EncryptValue_ExplicitNewlines()
    {
        const string value = "line 1\nline 2\nline 3";

        string result = DotenvxHelpers.EncryptValue(value, PublicKey);
        result.ShouldStartWith("encrypted:");

        string? decrypted = DotenvxHelpers.DecryptKeyValue("KEY", result, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe(value);
    }
}