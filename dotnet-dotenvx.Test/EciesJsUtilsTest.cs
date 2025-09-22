using dotnet_dotenvx.Logic;
using Org.BouncyCastle.Security;
using Shouldly;

namespace dotnet_dotenvx.Test;

public class EciesJsUtilsTest
{
    [Fact]
    public void Aes_RandomKey()
    {
        var key = new byte[32];
        var random = new SecureRandom();
        random.NextBytes(key);
        var data = "this is a test"u8.ToArray();
        var encrypted = EciesJsUtils.AesEncrypt(key, data);
        var decrypted = EciesJsUtils.AesDecrypt(key, encrypted);
        decrypted.ShouldBe(data);
    }

    [Fact]
    public void Aes_KnownKeyAndText()
    {
        var key = EciesJsUtils.DecodeHex("0000000000000000000000000000000000000000000000000000000000000000");
        var nonce = EciesJsUtils.DecodeHex("f3e1ba810d2c8900b11312b7c725565f");
        var tag = EciesJsUtils.DecodeHex("ec3b71e17c11dbe31484da9450edcf6c");
        var encrypted = EciesJsUtils.DecodeHex("02d2ffed93b856f148b9");

        byte[] data = [..nonce, ..tag, ..encrypted];
        var decrypted = EciesJsUtils.AesDecrypt(key, data);
        decrypted.ShouldBe("helloworld"u8.ToArray());
    }

    [Fact]
    public void DecodeHex_Handle0x()
    {
        var decoded = EciesJsUtils.DecodeHex("0x0011");
        decoded.ShouldBe([0, 0x11]);
    }
    
    [Fact]
    public void DecodeHex_KnownLowerCase()
    {
        var decoded = EciesJsUtils.DecodeHex("48656c6c6f20576f726c6421");
        decoded.ShouldBe("Hello World!"u8.ToArray());
    }
    
    [Fact]
    public void DecodeHex_KnownUpperCase()
    {
        var decoded = EciesJsUtils.DecodeHex("48656C6C6F20576F726C6421");
        decoded.ShouldBe("Hello World!"u8.ToArray());
    }
    
    [Fact]
    public void EncodeHex()
    {
        var bytes = "Hello World!"u8.ToArray();
        var encoded = EciesJsUtils.EncodeHex(bytes);
        encoded.ShouldBe("48656c6c6f20576f726c6421");
    }
}