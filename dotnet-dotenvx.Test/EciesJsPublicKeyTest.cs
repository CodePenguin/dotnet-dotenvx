using System.Security.Cryptography;
using dotnet_dotenvx.Logic;
using Org.BouncyCastle.Utilities.Encoders;
using Shouldly;

namespace dotnet_dotenvx.Test;

public class EciesJsPublicKeyTest
{
    private const string AlicePrivateKeyHex = "1e99423a4ed27608a15a2616dc0e1ebaefa58247e9c5b882acf98560e1d8e8e5";
    private const string AlicePublicKeyHex = "029a168fc647dc0c6c1e921ecf32b61263b6779ccd37f248de507f97d52ae1830d";
    private const string BobPrivateKeyHex = "f3b9cac2fc632551f1d1e3e7e4a236e1d44e0dce799d5145f18fa604df28b5f1";
    private const string BobPublicKeyHex = "0374dd2e8ab51fe05b27f026f894e42b6a42d8c00caeec7b542cb8a0bb7b2dd969";
    
    private static readonly byte[] AlicePrivateKey = Convert.FromHexString(AlicePrivateKeyHex);
    private static readonly byte[] BobPrivateKey = Convert.FromHexString(BobPrivateKeyHex);
    private static readonly byte[] AlicePublicKey = Convert.FromHexString(AlicePublicKeyHex);
    private static readonly byte[] BobPublicKey = Convert.FromHexString(BobPublicKeyHex);
    
    [Theory]
    // Calculated by eciesjs 0.4.15 Buffer.from(AlicePrivateKey.encapsulate(BobPublicKey, false)).toString("hex")
    [InlineData(
        AlicePrivateKeyHex, 
        BobPublicKeyHex,
        "b92f936db3d6edc56fe6f71deb78e21674eefb8b46eb08f1d18ea9c93256909a")]
    // Calculated by eciesjs 0.4.15 Buffer.from(BobPrivateKey.encapsulate(AlicePublicKey, false)).toString("hex")
    [InlineData(
        BobPrivateKeyHex, 
        AlicePublicKeyHex,
        "9208dc63cb2f85470216148e08a419daa4c60df3ae26c7a5fcd0aa9bd86eaf1a")]
    public void Ecdh_ComputesExpectedSharedSecret(string privateKeyHex, string peerPublicKeyHex, string expectedSharedHex)
    {
        var privateKey = Convert.FromHexString(privateKeyHex);
        var peerPublicKey = Convert.FromHexString(peerPublicKeyHex);
        var expectedShared = Convert.FromHexString(expectedSharedHex);

        var shared = EciesJsPublicKey.Decapsulate(peerPublicKey, privateKey);
        Assert.Equal(expectedShared, shared);
    }

    [Fact]
    public void Ecdh_ComputesSameSharedSecret()
    {
        // Calculated by eciesjs 0.4.15 Buffer.from(BobPrivateKey.encapsulate(AlicePublicKey, false)).toString("hex")
        var aliceShared = EciesJsPrivateKey.Encapsulate(AlicePrivateKey, BobPublicKey);
        // Calculated by eciesjs 0.4.15 Buffer.from(BobPublicKey.decapsulate(AlicePrivateKey, false)).toString("hex")
        var bobShared = EciesJsPublicKey.Decapsulate(AlicePublicKey, BobPrivateKey);
        aliceShared.ShouldBe(bobShared);
    }

    [Fact]
    public void Ecdh_Invalid_PublicKey_Throws()
    {
        var badPublicKey = new byte[33];
        badPublicKey[0] = 0x01; // invalid prefix
        Should.Throw<ArgumentException>(() => EciesJsPublicKey.Decapsulate(badPublicKey, AlicePrivateKey));
    }

    [Fact]
    public void Ecdh_Invalid_PrivateKey_Throws()
    {
        var badPrivateKey = new byte[10]; // too short
        Should.Throw<ArgumentException>(() => EciesJsPublicKey.Decapsulate(BobPublicKey, badPrivateKey));
    }

    [Fact]
    public void Ecdh_Returns32Bytes()
    {
        var secret = EciesJsPublicKey.Decapsulate(BobPublicKey, AlicePrivateKey);
        secret.Length.ShouldBe(32);
    }
}