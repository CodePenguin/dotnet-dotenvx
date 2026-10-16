using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Custodians;

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/resolveLocalKey.js. Not from a JS test (upstream covers it through the
/// custody/armor action tests); these run the real resolver against files in a temporary directory.
/// </remarks>
[Collection("Fsx")]
public class ResolveLocalKeyTest : EnvironmentTestBase
{
    private const string PrivateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-rlk-" + Guid.NewGuid().ToString("N"));

    public ResolveLocalKeyTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
    }

    ~ResolveLocalKeyTest()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private static string PublicKey => global::Dotenvx.Primitives.Primitives.Derive(PrivateKey);

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    [Fact]
    public void ResolvesAKeyHeldInEnvKeysAndRemovesItFromThere()
    {
        Write(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\nHELLO=\"encrypted:x\"\n");
        Write(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\nOTHER=1\n");

        LocalKey key = ResolveLocalKey.Resolve();

        key.PublicKey.ShouldBe(PublicKey);
        key.PrivateKey.ShouldBe(PrivateKey);
        key.PrivateKeyName.ShouldBe("DOTENV_PRIVATE_KEY");

        key.Remove();

        File.ReadAllText(Path.Combine(_dir, ".env.keys")).ShouldNotContain("DOTENV_PRIVATE_KEY");
    }

    [Fact]
    public void DerivesThePublicKeyWhenTheEnvFileHasNone()
    {
        Write(".env", "HELLO=World\n");
        Write(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");

        ResolveLocalKey.Resolve().PublicKey.ShouldBe(PublicKey);
    }

    [Fact]
    public void RejectsAPrivateKeyThatDoesNotMatchThePublicKey()
    {
        Write(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");
        Write(".env.keys", "DOTENV_PRIVATE_KEY=1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70\n");

        Should.Throw<Exception>(() => ResolveLocalKey.Resolve()).Message.ShouldBe("private key does not match the .env public key");
    }

    [Fact]
    public void RemoveRefusesWhenTheKeyChangedAfterItWasResolved()
    {
        Write(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");
        Write(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");
        LocalKey key = ResolveLocalKey.Resolve();
        Write(".env.keys", "DOTENV_PRIVATE_KEY=changed\n");

        Should.Throw<Exception>(key.Remove).Message.ShouldBe("private key changed during upload; original storage was not modified");
    }

    [Fact]
    public void ReportsAKeyThatIsHeldNowhere()
    {
        Write(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");

        Should.Throw<Exception>(() => ResolveLocalKey.Resolve()).Message
            .ShouldBe("private key DOTENV_PRIVATE_KEY not found in .env.keys, OS secret storage, 1Password, or Bitwarden");
    }

    [Fact]
    public void ReportsAMissingEnvKeysFileWhenThereIsNoEnvFileEither()
    {
        Should.Throw<MissingEnvFileException>(() => ResolveLocalKey.Resolve()).Message.ShouldContain(".env.keys");
    }

    [Fact]
    public void AllowMissingReturnsTheKeyWithoutAPrivateKeyWhenOnlyThePublicKeyIsKnown()
    {
        Write(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");

        LocalKey key = ResolveLocalKey.Resolve(allowMissing: true);

        key.PublicKey.ShouldBe(PublicKey);
        key.PrivateKey.ShouldBeNull();
    }

    [Fact]
    public void AllowMissingStillRequiresAPublicKey()
    {
        Should.Throw<MissingEnvFileException>(() => ResolveLocalKey.Resolve(allowMissing: true)).Message.ShouldContain(".env");

        Write(".env", "HELLO=World\n");

        Should.Throw<MissingKeyException>(() => ResolveLocalKey.Resolve(allowMissing: true)).Message.ShouldContain("DOTENV_PUBLIC_KEY");
    }

    [Fact]
    public void ResolvesTheKeysFileNextToANestedEnvFile()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "app"));
        Write("app/.env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");
        Write("app/.env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");

        ResolveLocalKey.Resolve("app/.env").PrivateKey.ShouldBe(PrivateKey);
    }
}
