using Dotenvx.Interop.EciesJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.EciesJs;

public class PrivateKeyTest : EciesJsTestBase
{
    [Theory]
    // Calculated by eciesjs 0.4.15 Buffer.from(AlicePrivateKey.encapsulate(BobPublicKey, false)).toString("hex")
    [InlineData(
        BobPrivateKeyHex,
        AlicePublicKeyHex,
        "b92f936db3d6edc56fe6f71deb78e21674eefb8b46eb08f1d18ea9c93256909a")]
    // Calculated by eciesjs 0.4.15 Buffer.from(BobPrivateKey.encapsulate(AlicePublicKey, false)).toString("hex")
    [InlineData(
        AlicePrivateKeyHex,
        BobPublicKeyHex,
        "9208dc63cb2f85470216148e08a419daa4c60df3ae26c7a5fcd0aa9bd86eaf1a")]
    public void TestEcdhComputesExpectedSharedSecret(string privateKeyHex, string peerPublicKeyHex,
        string expectedSharedHex)
    {
        byte[] peerPublicKey = Convert.FromHexString(peerPublicKeyHex);
        byte[] expectedShared = Convert.FromHexString(expectedSharedHex);

        byte[] shared = PrivateKey.FromHex(privateKeyHex).Encapsulate(peerPublicKey);
        Assert.Equal(expectedShared, shared);
    }

    [Fact]
    public void TestEcdhComputesSameSharedSecret()
    {
        // Calculated by eciesjs 0.4.15 Buffer.from(AlicePrivateKey.encapsulate(BobPublicKey, false)).toString("hex")
        byte[] aliceShared = PrivateKey.FromHex(AlicePrivateKeyHex).Encapsulate(Convert.FromHexString(BobPublicKeyHex));
        // Calculated by eciesjs 0.4.15 Buffer.from(AlicePublicKey.decapsulate(BobPrivateKey, false)).toString("hex")
        byte[] bobShared = PublicKey.FromHex(AlicePublicKeyHex).Decapsulate(Convert.FromHexString(BobPrivateKeyHex));
        aliceShared.ShouldBe(bobShared);
    }

    [Fact]
    public void TestEcdhInvalidPublicKeyThrows()
    {
        byte[] badPublicKey = new byte[33];
        badPublicKey[0] = 0x01; // invalid prefix
        Should.Throw<ArgumentException>(() => PrivateKey.FromHex(AlicePrivateKeyHex).Encapsulate(badPublicKey));
    }

    [Fact]
    public void TestEcdhInvalidPrivateKeyThrows()
    {
        byte[] badPrivateKey = new byte[10]; // too short
        Should.Throw<ArgumentException>(() => new PrivateKey(badPrivateKey));
    }

    [Fact]
    public void TestEcdhReturns32Bytes()
    {
        byte[] secret = PrivateKey.FromHex(AlicePrivateKeyHex).Encapsulate(Convert.FromHexString(BobPublicKeyHex));
        secret.Length.ShouldBe(32);
    }

    [Fact]
    public void TestGetPublicKeyCompressedShouldMatchNodeJsOutput()
    {
        // Calculated by eciesjs v0.4.15: Buffer.from(AlicePrivateKey.publicKey.toBytes(true)).toString("hex")
        byte[] alicePublicKey = PrivateKey.FromHex(AlicePrivateKeyHex).PublicKey.ToBytes();
        string alicePublicKeyHex = Convert.ToHexString(alicePublicKey).ToLowerInvariant();
        alicePublicKey.Length.ShouldBe(33);
        alicePublicKeyHex.ShouldBe("029a168fc647dc0c6c1e921ecf32b61263b6779ccd37f248de507f97d52ae1830d");

        // Calculated by eciesjs v0.4.15: Buffer.from(BobPrivateKey.publicKey.toBytes(true)).toString("hex")
        byte[] bobPublicKey = PrivateKey.FromHex(BobPrivateKeyHex).PublicKey.ToBytes();
        string bobPublicKeyHex = Convert.ToHexString(bobPublicKey).ToLowerInvariant();
        bobPublicKey.Length.ShouldBe(33);
        bobPublicKeyHex.ShouldBe("0374dd2e8ab51fe05b27f026f894e42b6a42d8c00caeec7b542cb8a0bb7b2dd969");
    }

    [Fact]
    public void TestGetPublicKeyUncompressedShouldMatchNodeJsOutput()
    {
        // Calculated by eciesjs v0.4.15: Buffer.from(AlicePrivateKey.publicKey.toBytes(false)).toString("hex")
        byte[] alicePublicKey = PrivateKey.FromHex(AlicePrivateKeyHex).PublicKey.ToBytes(false);
        string alicePublicKeyHex = Convert.ToHexString(alicePublicKey).ToLowerInvariant();
        alicePublicKey.Length.ShouldBe(65);
        alicePublicKeyHex.ShouldBe(
            "049a168fc647dc0c6c1e921ecf32b61263b6779ccd37f248de507f97d52ae1830ddc679da2dc5c79c0677deb11ec07f8274a106ad6c784bb4e75379b67c8d7b32e");

        // Calculated by eciesjs v0.4.15: Buffer.from(BobPrivateKey.publicKey.toBytes(false)).toString("hex")
        byte[] bobPublicKey = PrivateKey.FromHex(BobPrivateKeyHex).PublicKey.ToBytes(false);
        string bobPublicKeyHex = Convert.ToHexString(bobPublicKey).ToLowerInvariant();
        bobPublicKey.Length.ShouldBe(65);
        bobPublicKeyHex.ShouldBe(
            "0474dd2e8ab51fe05b27f026f894e42b6a42d8c00caeec7b542cb8a0bb7b2dd96916060bacc1b174e64e1ed92dc6110bfd75b1a028e7c423b467b07020126a154f");
    }
}