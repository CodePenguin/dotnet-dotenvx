using System.Text;

using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{armoredKeyDisplay,upsertEnvKey}.js and
/// dotenvx/src/cli/actions/lock/{up,down}.js. Not from a JS test - upstream has none for these; scrypt vectors were
/// generated with Node's `crypto.scryptSync(password, salt, 32)`.
/// </remarks>
public class LockHelpersTest : EnvironmentTestBase
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    [Theory]
    [InlineData("pw", 1, "67680113b2fde6cb5c7df741b09229e5324278cb3cf3b4c0d0b1fbde0a76baf8")]
    [InlineData("", 7, "fb727d659f9404d0416cb81ac50125c32b018d5b8a8b67043d08d9ca81052a9e")]
    public void ScryptMatchesNode(string password, byte saltByte, string expectedHex)
    {
        byte[] salt = Enumerable.Repeat(saltByte, 16).ToArray();

        Convert.ToHexStringLower(LockCrypto.Scrypt(Encoding.UTF8.GetBytes(password), salt, 16384, 8, 1, 32))
            .ShouldBe(expectedHex);
    }

    [Fact]
    public void LockedValueRoundTrips()
    {
        string locked = LockCrypto.LockedValue(PrivateKey, "pässword 🔑", PublicKey);

        locked.ShouldStartWith($"locked:{PublicKey}:");
        LockCrypto.UnlockedValue(locked, "pässword 🔑").ShouldBe(PrivateKey);
    }

    [Fact]
    public void UnlockWithWrongPassphraseThrows()
    {
        string locked = LockCrypto.LockedValue(PrivateKey, "pw", PublicKey);

        Should.Throw<Exception>(() => LockCrypto.UnlockedValue(locked, "bad"));
    }

    [Fact]
    public void UnlockRejectsUnsupportedVersion()
    {
        string payload = LockCrypto.Base64Url([2, .. new byte[60]]);

        Should.Throw<InvalidOperationException>(() => LockCrypto.UnlockedValue($"locked:{PublicKey}:{payload}", "pw"))
            .Message.ShouldBe("unsupported locked private key version");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("ab", "AB")]
    [InlineData("abc", "ABC")]
    [InlineData("02b106c305", "02B 106")]
    public void ArmoredKeyDisplayGroupsTheFirstSixCharacters(string? publicKey, string expected)
    {
        ArmoredKeyDisplay.Display(publicKey).ShouldBe(expected);
    }

    [Fact]
    public void UpsertEnvKeyCreatesReplacesAndPreservesEol()
    {
        string path = Path.Combine(Path.GetTempPath(), "dotenvx-upsert-" + Guid.NewGuid().ToString("N"));
        try
        {
            UpsertEnvKeyResult created = UpsertEnvKey("A", "1", path);
            created.Created.ShouldBeTrue();
            created.Changed.ShouldBeTrue();
            File.ReadAllText(path).ShouldBe("A=1\n");

            UpsertEnvKey("A", "1", path).Changed.ShouldBeFalse();

            File.WriteAllText(path, "# c\r\nexport A = 0\r\nB=2\r\nA=dup\r\n\r\n");
            UpsertEnvKey("A", "9", path).Changed.ShouldBeTrue();
            File.ReadAllText(path).ShouldBe("# c\r\nA=9\r\nB=2\r\n");

            UpsertEnvKey("C", "3", path);
            File.ReadAllText(path).ShouldBe("# c\r\nA=9\r\nB=2\r\nC=3\r\n");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
