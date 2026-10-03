using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/encrypt.js and transforms/set.js (no upstream tests exist at this
/// pin). Covers how the transforms treat keys: the public key is `publickeys(src)[0]`, a keypair is only made
/// when there is none, the private key goes into the returned `KeysSrc` (the caller writes it), and the keys
/// file defaults to `.env.keys` in the working directory.
/// </remarks>
public class TransformKeysTest : EnvironmentTestBase
{
    private const string KeysHeader =
        "#/------------------!DOTENV_PRIVATE_KEYS!-------------------/\n" +
        "#/ private decryption keys. DO NOT commit to source control /\n" +
        "#/     [how it works](https://dotenvx.com/encryption)       /\n" +
        "#/          ⛨ ARMORED KEYS: `dotenvx armor up`              /\n" +
        "#/----------------------------------------------------------/\n";

    private static EnvItem[] Env(string file)
    {
        return [new EnvItem { Type = EnvType.EnvFile, Value = file }];
    }

    /// <remarks>Not from a JS test - a file with no public key gets a fresh keypair; the private key goes to KeysSrc under the standard header.</remarks>
    [Fact]
    public void TestEncryptGeneratesKeypairIntoKeysSrc()
    {
        const string envFile = "tests/monorepo/apps/frontend/.env";
        const string keysFile = "tests/monorepo/apps/frontend/new.keys";

        EncryptResult result = new Encrypt(Env(envFile), null, null, keysFile).Run();

        EncryptProcessedEnv row = result.ProcessedEnvs.ShouldHaveSingleItem();
        string publicKey = PublicKeys(row.EnvSrc).ShouldHaveSingleItem();
        result.KeysSrc.ShouldNotBeNull();
        result.KeysSrc.ShouldStartWith(KeysHeader);
        string privateKey = result.KeysSrc.Split('\n').Single(l => l.StartsWith("DOTENV_PRIVATE_KEY=")).Split('=')[1];
        Derive(privateKey).ShouldBe(publicKey);
        result.KeysSrc.ShouldContain("# .env\n");
    }

    /// <remarks>Not from a JS test - the first DOTENV_PUBLIC_KEY* in the file is used whatever its name, and no keypair is generated.</remarks>
    [Fact]
    public void TestEncryptUsesFirstPublicKeyWhateverItsName()
    {
        (string publicKey, string privateKey) = Keypair();
        string envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        File.WriteAllText(envFile, $"DOTENV_PUBLIC_KEY_PRODUCTION=\"{publicKey}\"\nA=1\n");

        EncryptResult result = new Encrypt(Env(envFile), null, null, null).Run();

        EncryptProcessedEnv row = result.ProcessedEnvs.ShouldHaveSingleItem();
        row.Error.ShouldBeNull();
        result.KeysSrc.ShouldBeNull();
        PublicKeys(row.EnvSrc).ShouldBe([publicKey]);
        string value = Scan(row.EnvSrc, new ScanOptions()).Parsed["A"].Single();
        Decrypt(privateKey, value).ShouldBe("1");
    }

    /// <remarks>Not from a JS test - an existing keys file is kept and the new private key is appended to it.</remarks>
    [Fact]
    public void TestEncryptAppendsToExistingKeysSrc()
    {
        const string envFile = "tests/monorepo/apps/frontend/.env";
        string keysFile = Path.Combine(Directory.GetCurrentDirectory(), ".env.keys");
        File.WriteAllText(keysFile, "# existing\nOTHER=1\n");

        EncryptResult result = new Encrypt(Env(envFile), null, null, null).Run();

        result.KeysSrc.ShouldNotBeNull();
        result.KeysSrc.ShouldStartWith("# existing\nOTHER=1\n\n# .env\nDOTENV_PRIVATE_KEY=");
    }

    /// <remarks>Not from a JS test - a key ending in _PLAIN and public key entries are never encrypted; `ik` limits what is.</remarks>
    [Fact]
    public void TestEncryptSkipsPlainKeysAndHonorsIncludeKeys()
    {
        (string publicKey, string _) = Keypair();
        string envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        File.WriteAllText(envFile, $"DOTENV_PUBLIC_KEY=\"{publicKey}\"\nA=1\nB=2\nC_PLAIN=3\n");

        EncryptResult result = new Encrypt(Env(envFile), ["A"], null, null).Run();

        EncryptProcessedEnv row = result.ProcessedEnvs.ShouldHaveSingleItem();
        row.Keys.ShouldBe(["A"]);
        Dictionary<string, List<string>> parsed = Scan(row.EnvSrc, new ScanOptions()).Parsed;
        parsed["A"].Single().ShouldStartWith("encrypted:");
        parsed["B"].ShouldBe(["2"]);
        parsed["C_PLAIN"].ShouldBe(["3"]);
    }

    /// <remarks>Not from a JS test - set only generates a keypair when it is going to encrypt; plaintext sets leave KeysSrc empty.</remarks>
    [Fact]
    public void TestSetPlainDoesNotGenerateKeys()
    {
        const string envFile = "tests/monorepo/apps/frontend/.env";

        SetsResult result = new Sets("KEY", "value", Env(envFile), false).Run();

        result.KeysSrc.ShouldBeNull();
        PublicKeys(result.ProcessedEnvs.ShouldHaveSingleItem().EnvSrc).ShouldBeEmpty();
    }

    /// <remarks>Not from a JS test - setting the value a key already has (compared after decryption through the get resolver) is "no change" and leaves the ciphertext alone.</remarks>
    [Fact]
    public void TestSetSameDecryptedValueIsNoChange()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string keysFile = "tests/monorepo/apps/encrypted/.env.keys";

        SetsResult result = new Sets("HELLO", "encrypted", Env(envFile), true, keysFile).Run();

        ProcessedSet row = result.ProcessedEnvs.ShouldHaveSingleItem();
        row.Error.ShouldBeNull();
        row.Changed.ShouldBeFalse();
        result.UnchangedFilePaths.ShouldBe([envFile]);
    }
}
