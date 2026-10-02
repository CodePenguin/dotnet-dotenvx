using Dotenvx.Primitives.Interop.EciesJs;

using Org.BouncyCastle.Security;

using Shouldly;

namespace Dotenvx.Primitives.Test.Interop.EciesJs;

public class UtilsTest
{
    [Fact]
    public void Aes_RandomKey()
    {
        byte[] key = new byte[32];
        SecureRandom random = new();
        random.NextBytes(key);
        byte[] data = "this is a test"u8.ToArray();
        byte[] encrypted = Utils.AesEncrypt(key, data);
        byte[] decrypted = Utils.AesDecrypt(key, encrypted);
        decrypted.ShouldBe(data);
    }

    [Fact]
    public void Aes_KnownKeyAndText()
    {
        byte[] key = Utils.DecodeHex("0000000000000000000000000000000000000000000000000000000000000000");
        byte[] nonce = Utils.DecodeHex("f3e1ba810d2c8900b11312b7c725565f");
        byte[] tag = Utils.DecodeHex("ec3b71e17c11dbe31484da9450edcf6c");
        byte[] encrypted = Utils.DecodeHex("02d2ffed93b856f148b9");

        byte[] data = [..nonce, ..tag, ..encrypted];
        byte[] decrypted = Utils.AesDecrypt(key, data);
        decrypted.ShouldBe("helloworld"u8.ToArray());
    }

    [Fact]
    public void DecodeHex_Handle0x()
    {
        byte[] decoded = Utils.DecodeHex("0x0011");
        decoded.ShouldBe([0, 0x11]);
    }

    [Fact]
    public void DecodeHex_KnownLowerCase()
    {
        byte[] decoded = Utils.DecodeHex("48656c6c6f20576f726c6421");
        decoded.ShouldBe("Hello World!"u8.ToArray());
    }

    [Fact]
    public void DecodeHex_KnownUpperCase()
    {
        byte[] decoded = Utils.DecodeHex("48656C6C6F20576F726C6421");
        decoded.ShouldBe("Hello World!"u8.ToArray());
    }
}