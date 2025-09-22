using System.Security.Cryptography;
using dotnet_dotenvx.Logic;
using Org.BouncyCastle.Utilities.Encoders;
using Shouldly;

namespace dotnet_dotenvx.Test;

public class Secp256k1JsTest
{
    private const string AlicePrivateKeyHex = "1E99423A4ED27608A15A2616DC0E1EBAEFA58247E9C5B882ACF98560E1D8E8E5";
    private const string BobPrivateKeyHex = "F3B9CAC2FC632551F1D1E3E7E4A236E1D44E0DCE799D5145F18FA604DF28B5F1";

    private readonly byte[] _alicePrivateKey = Hex.Decode(AlicePrivateKeyHex);
    private readonly byte[] _bobPrivateKey = Hex.Decode(BobPrivateKeyHex);
    private readonly byte[] _alicePublicKey = Secp256k1Js.GetPublicKey(Hex.Decode(AlicePrivateKeyHex), false);
    private readonly byte[] _bobPublicKey = Secp256k1Js.GetPublicKey(Hex.Decode(BobPrivateKeyHex), false);

    [Fact]
    public void Ecdh_ComputesExpectedSharedSecret()
    {
        // Calculated by Secpk256k1 v3.6.0: secpk256k1.ecdh(bobPublicKey, alicePrivateKey).toString('hex')
        var expectedSharedSecret = Hex.Decode("ea732b6128fa6b70fe84052e64b474f1eccde913d1e3d12d20e132590b96ce82");
        var aliceShared = Secp256k1Js.Ecdh(_bobPublicKey, _alicePrivateKey);
        var bobShared = Secp256k1Js.Ecdh(_alicePublicKey, _bobPrivateKey);
        aliceShared.ShouldBe(bobShared);
        EciesJsUtils.EncodeHex(aliceShared).ShouldBe(EciesJsUtils.EncodeHex(expectedSharedSecret));
    }

    [Fact]
    public void Ecdh_ComputesSameSharedSecret()
    {
        var aliceShared = Secp256k1Js.Ecdh(_bobPublicKey, _alicePrivateKey);
        var bobShared = Secp256k1Js.Ecdh(_alicePublicKey, _bobPrivateKey);
        aliceShared.ShouldBe(bobShared);
    }

    [Fact]
    public void Ecdh_Invalid_PublicKey_Throws()
    {
        var badPublicKey = new byte[33];
        badPublicKey[0] = 0x01; // invalid prefix
        Should.Throw<ArgumentException>(() => Secp256k1Js.Ecdh(badPublicKey, _alicePrivateKey));
    }

    [Fact]
    public void Ecdh_Invalid_PrivateKey_Throws()
    {
        var badPrivateKey = new byte[10]; // too short
        Should.Throw<ArgumentException>(() => Secp256k1Js.Ecdh(_bobPublicKey, badPrivateKey));
    }

    [Fact]
    public void Ecdh_Returns32Bytes()
    {
        var secret = Secp256k1Js.Ecdh(_bobPublicKey, _alicePrivateKey);
        secret.Length.ShouldBe(32);
    }
    
    [Theory]
    [InlineData(
        "1E99423A4ED27608A15A2616A2B0E9E58EDE3B8E4F77AB2C0B8CD43C5E5CF59B", 
        "032C8C31FC9F990C6B55E3865A184A4CE50E09481F2EAEB3E60EC1CEA13A6AE645",
        "07607293075974BA0A22F282F590DA9F4A8D04C20982432123E6A6D8ED5A48EB")]
    [InlineData(
        "C9AFA9D845BA75166B5C215767B1D6934E50C3DB36E89B127B8A622B120F6721", 
        "02569F693802CE53DEFAFFD37DFF4FEFACEDA0358CA62229023F18C4826F1AC132",
        "07607293075974BA0A22F282F590DA9F4A8D04C20982432123E6A6D8ED5A48EB")]
    public void Ecdh_TwoParty_ShouldMatchNode(string privateKeyHex, string peerPublicKeyHex, string expectedSharedHex)
    {
        var privateKey = Convert.FromHexString(privateKeyHex);
        var peerPublicKey = Convert.FromHexString(peerPublicKeyHex);
        var expectedShared = Convert.FromHexString(expectedSharedHex);

        var shared = Secp256k1Js.Ecdh(peerPublicKey, privateKey);
        Assert.Equal(expectedShared, shared);
    }
    
    [Fact]
    public void GetPublicKey_Compressed_ShouldMatchNodeJsOutput()
    {
        // Calculated by Secpk256k1 v3.6.0: secp256k1.publicKeyCreate(_alicePrivateKey, true).toString('hex')
        var alicePublicKey = Secp256k1Js.GetPublicKey(_alicePrivateKey, true);
        var alicePublicKeyHex = EciesJsUtils.EncodeHex(alicePublicKey);
        alicePublicKey.Length.ShouldBe(33);
        alicePublicKeyHex.ShouldBe("029a168fc647dc0c6c1e921ecf32b61263b6779ccd37f248de507f97d52ae1830d");

        // Calculated by Secpk256k1 v3.6.0: secp256k1.publicKeyCreate(_bobPrivateKey, true).toString('hex')
        var bobPublicKey = Secp256k1Js.GetPublicKey(_bobPrivateKey, true);
        var bobPublicKeyHex = EciesJsUtils.EncodeHex(bobPublicKey);
        bobPublicKey.Length.ShouldBe(33);
        bobPublicKeyHex.ShouldBe("0374dd2e8ab51fe05b27f026f894e42b6a42d8c00caeec7b542cb8a0bb7b2dd969");
    }

    [Fact]
    public void GetPublicKey_Uncompressed_ShouldMatchNodeJsOutput()
    {
        // Calculated by Secpk256k1 v3.6.0: secp256k1.publicKeyCreate(_alicePrivateKey, false).toString('hex')
        var alicePublicKey = Secp256k1Js.GetPublicKey(_alicePrivateKey, false);
        var alicePublicKeyHex = EciesJsUtils.EncodeHex(alicePublicKey);
        alicePublicKey.Length.ShouldBe(65);
        alicePublicKeyHex.ShouldBe("049a168fc647dc0c6c1e921ecf32b61263b6779ccd37f248de507f97d52ae1830ddc679da2dc5c79c0677deb11ec07f8274a106ad6c784bb4e75379b67c8d7b32e");

        // Calculated by Secpk256k1 v3.6.0: secp256k1.publicKeyCreate(_bobPrivateKey, false).toString('hex')
        var bobPublicKey = Secp256k1Js.GetPublicKey(_bobPrivateKey, false);
        var bobPublicKeyHex = EciesJsUtils.EncodeHex(bobPublicKey);
        bobPublicKey.Length.ShouldBe(65);
        bobPublicKeyHex.ShouldBe("0474dd2e8ab51fe05b27f026f894e42b6a42d8c00caeec7b542cb8a0bb7b2dd96916060bacc1b174e64e1ed92dc6110bfd75b1a028e7c423b467b07020126a154f");
    }
}