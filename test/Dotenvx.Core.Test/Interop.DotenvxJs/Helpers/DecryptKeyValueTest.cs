using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/decrypt-key-value.test.js</remarks>
public class DecryptKeyValueTest
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    /// <remarks>Refer to "#decryptKeyValue with valid input"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithValidInputDecryptsValue()
    {
        string encryptedString = EncryptValue("hello", PublicKey);
        string? decrypted = DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("hello");
    }

    /// <remarks>Refer to "#decryptKeyValue with null private key"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithNullPrivateKeyThrowsException()
    {
        string encryptedString = EncryptValue("hello", PublicKey);

        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY_PRODUCTION", null));

        ex.Code.ShouldBe("MISSING_PRIVATE_KEY");
        ex.Message.ShouldBe(
            "[MISSING_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY_PRODUCTION='");
        ex.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/464]");
    }

    /// <remarks>Refer to "#decryptKeyValue with non-encrypted value"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithNonEncryptedValueReturnsOriginalValue()
    {
        string? decrypted = DecryptKeyValue("KEY", "world", "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("world");
    }

    /// <remarks>Refer to "#decryptKeyValue with invalid short encrypted value"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithInvalidShortEncryptedValueThrowsException()
    {
        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY", "encrypted:1234", "DOTENV_PRIVATE_KEY", PrivateKey));

        ex.Code.ShouldBe("DECRYPTION_FAILED");
        ex.Message.ShouldBe("[DECRYPTION_FAILED] second arg must be public key");
    }

    /// <remarks>Refer to "#decryptKeyValue with malformed encrypted value"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithMalformedEncryptedValueThrowsException()
    {
        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY",
                "encrypted:ADJIvD6DxJdTcFdg1tcasYa9G1O5YVtFJs0yJgem+aGIlRJl9N1Fbq6kdPtIwfS0c6VJF4EN6H+D0JUwJ4FmoerQi0XQ4mv4AyA73KjrxVEqmSypg2InsV0e4WxdP5Qx/jVVSgxD",
                "DOTENV_PRIVATE_KEY", PrivateKey));

        ex.Code.ShouldBe("MALFORMED_ENCRYPTED_DATA");
        ex.Message.ShouldBe(
            "[MALFORMED_ENCRYPTED_DATA] could not decrypt KEY because encrypted data appears malformed");
    }

    /// <remarks>Refer to "#decryptKeyValue with empty encrypted value"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithEmptyEncryptedValueThrowsException()
    {
        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY", "encrypted:", "DOTENV_PRIVATE_KEY", PrivateKey));

        ex.Code.ShouldBe("DECRYPTION_FAILED");
        ex.Message.ShouldBe("[DECRYPTION_FAILED] second arg must be public key");
    }

    /// <remarks>Refer to "#decryptKeyValue with invalid private key"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithInvalidPrivateKeyThrowsException()
    {
        string encryptedString = EncryptValue("hello", PublicKey);

        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", "invalid-private-key"));

        ex.Code.ShouldBe("INVALID_PRIVATE_KEY");
        ex.Message.ShouldBe(
            "[INVALID_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY=invalid…'");
    }

    /// <remarks>Refer to "#decryptKeyValue with empty private key"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithEmptyPrivateKeyThrowsException()
    {
        string encryptedString = EncryptValue("hello", PublicKey);

        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", ""));

        ex.Code.ShouldBe("MISSING_PRIVATE_KEY");
        ex.Message.ShouldBe("[MISSING_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY='");
        ex.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/464]");
    }

    /// <remarks>Refer to "#decryptKeyValue with wrong private key"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithWrongPrivateKeyThrowsException()
    {
        string encryptedString = EncryptValue("hello", PublicKey);

        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY",
                "9c1ab41477004e68066129a8866887d316ba5d7177593dbc5e3026d6f64d32f8"));

        ex.Code.ShouldBe("WRONG_PRIVATE_KEY");
        ex.Message.ShouldBe(
            "[WRONG_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY=9c1ab41…'");
    }

    /// <remarks>Refer to "#decryptKeyValue with empty string"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithEmptyStringDecryptsValue()
    {
        string encryptedString = EncryptValue("", PublicKey);
        string? decrypted = DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("");
    }

    /// <remarks>Refer to "#decryptKeyValue with hardcoded scenario"</remarks>
    [Fact]
    public void TestDecryptKeyValueWithHardcodedScenarioDecryptsValue()
    {
        string? decrypted = DecryptKeyValue("KEY",
            "encrypted:BMVCQpz/+NYDcGZhbXyqbwP8IDJSTXl4xDQsgusQHEVFAWOXQnKRBTOzRiwuYIJzjuWnKkrQJEDEi8Av9xnfx61jVTJymVWLjVmFK7CM+6lmKOnIhPMzu0Mi0dH82P81bOXjkZTHIIcA",
            "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("expanded");
    }
}