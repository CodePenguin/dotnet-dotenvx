using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/keypair.js and dotenvx/tests/lib/resolvers/keypair.test.js.
/// The resolver is `publickeys(src)[0]` plus a primitives keyring (keys file next to the env file by default,
/// then the process environment), so the private key is only ever found for a public key the file declares.
/// </remarks>
public class KeyPairTest : EnvironmentTestBase
{
    private const string EnvFile = "tests/monorepo/apps/encrypted/.env";
    private const string KeysFile = "tests/monorepo/apps/encrypted/.env.keys";

    private static string FixturePublicKey()
    {
        return (string)ScanToDictionary(File.ReadAllText(EnvFile))["DOTENV_PUBLIC_KEY"];
    }

    private static string FixturePrivateKey()
    {
        return (string)ScanToDictionary(File.ReadAllText(KeysFile))["DOTENV_PRIVATE_KEY"];
    }

    /// <remarks>Not from a JS test - the default keys file is `.env.keys` next to the env file.</remarks>
    [Fact]
    public void TestRunFindsPublicKeyInFileAndPrivateKeyInKeysFileNextToIt()
    {
        Dictionary<string, string?> result = new KeyPair(EnvFile).Run();

        result["DOTENV_PUBLIC_KEY"].ShouldBe(FixturePublicKey());
        result["DOTENV_PRIVATE_KEY"].ShouldBe(FixturePrivateKey());
    }

    /// <remarks>Not from a JS test - an explicit keys file path overrides the default location.</remarks>
    [Fact]
    public void TestRunUsesCustomEnvKeysFile()
    {
        Dictionary<string, string?> result = new KeyPair(EnvFile, KeysFile).Run();

        result["DOTENV_PRIVATE_KEY"].ShouldBe(FixturePrivateKey());
    }

    /// <remarks>Not from a JS test - a private key exported in the environment is found through the keyring.</remarks>
    [Fact]
    public void TestRunFindsPrivateKeyInProcessEnvironment()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", FixturePrivateKey());

        Dictionary<string, string?> result = new KeyPair(EnvFile, "tests/monorepo/apps/encrypted/does-not-exist.keys").Run();

        result["DOTENV_PRIVATE_KEY"].ShouldBe(FixturePrivateKey());
    }

    /// <remarks>Not from a JS test - without a public key in the env file there is nothing to look up, so even a keys file with a private key yields null for both.</remarks>
    [Fact]
    public void TestRunReturnsNullsWhenTheEnvFileHasNoPublicKey()
    {
        Dictionary<string, string?> result = new KeyPair("tests/monorepo/apps/frontend/.env", KeysFile).Run();

        result["DOTENV_PUBLIC_KEY"].ShouldBeNull();
        result["DOTENV_PRIVATE_KEY"].ShouldBeNull();
    }

    /// <remarks>Not from a JS test - encrypt/set use this resolver to check for an existing key on a file that does not exist yet, so a missing file is not an error here.</remarks>
    [Fact]
    public void TestRunMissingEnvFileReturnsNulls()
    {
        Dictionary<string, string?> result = new KeyPair("tests/monorepo/apps/nope/.env").Run();

        result["DOTENV_PUBLIC_KEY"].ShouldBeNull();
        result["DOTENV_PRIVATE_KEY"].ShouldBeNull();
    }

    /// <remarks>Not from a JS test - key names follow the file name convention.</remarks>
    [Fact]
    public void TestRunNamesKeysAfterTheEnvironmentFile()
    {
        Dictionary<string, string?> result = new KeyPair("tests/monorepo/apps/frontend/.env.production").Run();

        result.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "DOTENV_PRIVATE_KEY_PRODUCTION"]);
    }
}
