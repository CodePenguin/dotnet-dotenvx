using Dotenvx.Core.Interop.Dotenvx.Types;

using Shouldly;

namespace Dotenvx.Test.Interop.Dotenvx.Helpers;

public class DotenvxTest
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    [Fact]
    public void DecryptKeyValue()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("hello", PublicKey);
        string? decrypted = Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("hello");
    }

    [Fact]
    public void DecryptKeyValue_EmptyEncryptedValueThrowsError()
    {
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", "encrypted:", "DOTENV_PRIVATE_KEY", PrivateKey));
        exception.Code.ShouldBe("DECRYPTION_FAILED");
        exception.Message.ShouldBe("second arg must be public key");
    }

    [Fact]
    public void DecryptKeyValue_EmptyPrivateKeyThrowsError()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("hello", PublicKey);
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", ""));
        exception.Code.ShouldBe("MISSING_PRIVATE_KEY");
        exception.Message.ShouldBe("[MISSING_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY='");
    }

    [Fact]
    public void DecryptKeyValue_HardCodedScenario()
    {
        string? decrypted = Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", "encrypted:BMVCQpz/+NYDcGZhbXyqbwP8IDJSTXl4xDQsgusQHEVFAWOXQnKRBTOzRiwuYIJzjuWnKkrQJEDEi8Av9xnfx61jVTJymVWLjVmFK7CM+6lmKOnIhPMzu0Mi0dH82P81bOXjkZTHIIcA", "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("expanded");
    }

    [Fact]
    public void DecryptKeyValue_InvalidEncryptedValueThrowsError()
    {
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", "encrypted:ADJIvD6DxJdTcFdg1tcasYa9G1O5YVtFJs0yJgem+aGIlRJl9N1Fbq6kdPtIwfS0c6VJF4EN6H+D0JUwJ4FmoerQi0XQ4mv4AyA73KjrxVEqmSypg2InsV0e4WxdP5Qx/jVVSgxD", "DOTENV_PRIVATE_KEY", PrivateKey));
        exception.Code.ShouldBe("MALFORMED_ENCRYPTED_DATA");
        exception.Message.ShouldBe("[MALFORMED_ENCRYPTED_DATA] could not decrypt KEY because encrypted data appears malformed");
    }

    [Fact]
    public void DecryptKeyValue_InvalidPrivateKeyThrowsError()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("hello", PublicKey);
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", "invalid-private-key"));
        exception.Code.ShouldBe("INVALID_PRIVATE_KEY");
        exception.Message.ShouldBe("[INVALID_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY=invalid…'");
    }

    [Fact]
    public void DecryptKeyValue_MissingPrefix()
    {
        string? decrypted = Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", "world", "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("world"); // return the original raw value
    }

    [Fact]
    public void DecryptKeyValue_PrivateKeyNull()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("hello", PublicKey);
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY_PRODUCTION", null));
        exception.Code.ShouldBe("MISSING_PRIVATE_KEY");
        exception.Message.ShouldBe("[MISSING_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY_PRODUCTION='");
    }

    [Fact]
    public void DecryptKeyValue_ShortEncryptedValueThrowsError()
    {
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", "encrypted:1234", "DOTENV_PRIVATE_KEY", PrivateKey));
        exception.Code.ShouldBe("DECRYPTION_FAILED");
        exception.Message.ShouldBe("second arg must be public key");
    }

    [Fact]
    public void DecryptKeyValue_WhenEmptyString()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("", PublicKey);
        string? decrypted = Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("");
    }

    [Fact]
    public void DecryptKeyValue_WrongPrivateKeyThrowsError()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("hello", PublicKey);
        DotenvxException exception = Should.Throw<DotenvxException>(() => Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", "9c1ab41477004e68066129a8866887d316ba5d7177593dbc5e3026d6f64d32f8"));
        exception.Code.ShouldBe("WRONG_PRIVATE_KEY");
        exception.Message.ShouldBe("[WRONG_PRIVATE_KEY] could not decrypt KEY using private key 'DOTENV_PRIVATE_KEY=9c1ab41…'");
    }

    [Fact]
    public void EncryptValue()
    {
        string encryptedString = Core.Interop.Dotenvx.Helpers.Dotenvx.EncryptValue("hello", PublicKey);
        string? decrypted = Core.Interop.Dotenvx.Helpers.Dotenvx.DecryptKeyValue("KEY", encryptedString, "DOTENV_PRIVATE_KEY", PrivateKey);
        decrypted.ShouldBe("hello");
    }
}