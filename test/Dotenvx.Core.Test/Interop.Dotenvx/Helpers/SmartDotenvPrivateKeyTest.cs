using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

public class SmartDotenvPrivateKeyTest : EnvironmentTestBase
{
    private const string Filepath = ".env";

    [Fact]
    public void SmartDotenvPrivateKey_ReturnsNull()
    {
        string? result = DotenvxHelpers.SmartDotenvPrivateKey(Filepath);

        result.ShouldBeNull();
    }

    [Fact]
    public void SmartDotenvPrivateKey_WhenProcessEnvDotenvPrivateKeyIsSet_ReturnsPrivateKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "<privateKey>");

        string? result = DotenvxHelpers.SmartDotenvPrivateKey(Filepath);

        result.ShouldBe("<privateKey>");
    }

    [Fact]
    public void SmartDotenvPrivateKey_WhenProcessEnvDotenvPrivateKeyIsSetButEmpty_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "");

        string? result = DotenvxHelpers.SmartDotenvPrivateKey(Filepath);

        result.ShouldBeNull();
    }

    [Fact]
    public void SmartDotenvPrivateKey_WhenEnvKeysPresent_ReturnsPrivateKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/.env";

        string? result = DotenvxHelpers.SmartDotenvPrivateKey(filepath);

        result.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    [Fact]
    public void SmartDotenvPrivateKey_WhenEnvKeysPresentButFilenameIsNotDotEnv_ReturnsPrivateKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/secrets.txt";

        string? result = DotenvxHelpers.SmartDotenvPrivateKey(filepath);

        result.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    [Fact]
    public void SmartDotenvPrivateKey_WhenDotenvPrivateKeyPassedAndCustomFilename_ReturnsPrivateKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "<privateKey>");

        const string filepath = "tests/monorepo/apps/encrypted/secrets.txt";

        string? result = DotenvxHelpers.SmartDotenvPrivateKey(filepath);

        result.ShouldBe("<privateKey>");
    }

    [Fact]
    public void
        SmartDotenvPrivateKey_WhenDotenvPrivateKeyPassedAndCustomFilenameButDifferentNamedPublicKey_ReturnsNullThenCorrect()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "<privateKey>");

        const string filepath = "tests/monorepo/apps/encrypted/secrets.ci.txt";

        string? result = DotenvxHelpers.SmartDotenvPrivateKey(filepath);
        result.ShouldBeNull(); // it should not find it because it is instead looking for a DOTENV_PRIVATE_KEY_CI

        // matching ci key is set - inverse of the ci public key in the secrets.ci.txt file
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY_CI", "<privateKeyCi>");
        string? result2 = DotenvxHelpers.SmartDotenvPrivateKey(filepath);
        result2.ShouldBe("<privateKeyCi>");

        // an additional random key is set but still with the dotenv private key schema
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY_PRODUCTION", "<privateKeyProduction>");
        string? result3 = DotenvxHelpers.SmartDotenvPrivateKey(filepath);
        result3.ShouldBe("<privateKeyCi>"); // it should still find the CI one first
    }
}