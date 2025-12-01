using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/smart-dotenv-private-key.test.js</remarks>
public class SmartDotenvPrivateKeyTest : EnvironmentTestBase
{
    private const string FilePath = ".env";

    /// <remarks>Refer to "#smartDotenvPrivateKey"</remarks>
    [Fact]
    public void TestSmartDotenvPrivateKeyReturnsNull()
    {
        string? result = SmartDotenvPrivateKey(FilePath);

        result.ShouldBeNull();
    }

    /// <remarks>Refer to "#smartDotenvPrivateKey when process.env.DOTENV_PRIVATE_KEY is set"</remarks>
    [Fact]
    public void TestSmartDotenvPrivateKeyWhenProcessEnvDotenvPrivateKeyIsSetReturnsPrivateKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "<privateKey>");

        string? result = SmartDotenvPrivateKey(FilePath);

        result.ShouldBe("<privateKey>");
    }

    /// <remarks>Refer to "#smartDotenvPrivateKey when process.env.DOTENV_PRIVATE_KEY is set but it is an empty string"</remarks>
    [Fact]
    public void TestSmartDotenvPrivateKeyWhenProcessEnvDotenvPrivateKeyIsSetButEmptyReturnsNull()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "");

        string? result = SmartDotenvPrivateKey(FilePath);

        result.ShouldBeNull();
    }

    /// <remarks>Refer to "#smartDotenvPrivateKey when .env.keys present"</remarks>
    [Fact]
    public void TestSmartDotenvPrivateKeyWhenEnvKeysPresentReturnsPrivateKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/.env";

        string? result = SmartDotenvPrivateKey(filepath);

        result.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    /// <remarks>
    ///     Refer to "#smartDotenvPrivateKey when .env.keys present but filename is not .env (use invertse of
    ///     DOTENV_PUBLIC_KEY* in the filename (if exists))"
    /// </remarks>
    [Fact]
    public void TestSmartDotenvPrivateKeyWhenEnvKeysPresentButFilenameIsNotDotEnvReturnsPrivateKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/secrets.txt";

        string? result = SmartDotenvPrivateKey(filepath);

        result.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    /// <remarks>Refer to "#smartDotenvPrivateKey when DOTENV_PRIVATE_KEY passed and custom filename"</remarks>
    [Fact]
    public void TestSmartDotenvPrivateKeyWhenDotenvPrivateKeyPassedAndCustomFilenameReturnsPrivateKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "<privateKey>");

        const string filepath = "tests/monorepo/apps/encrypted/secrets.txt";

        string? result = SmartDotenvPrivateKey(filepath);

        result.ShouldBe("<privateKey>");
    }

    /// <remarks>
    ///     Refer to "#smartDotenvPrivateKey when DOTENV_PRIVATE_KEY passed and custom filename but custom filename has a
    ///     different named DOTENV_PUBLIC_KEY"
    /// </remarks>
    [Fact]
    public void
        TestSmartDotenvPrivateKeyWhenDotenvPrivateKeyPassedAndCustomFilenameButDifferentNamedPublicKeyReturnsNullThenCorrect()
    {
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "<privateKey>");

        const string filepath = "tests/monorepo/apps/encrypted/secrets.ci.txt";

        string? result = SmartDotenvPrivateKey(filepath);
        result.ShouldBeNull(); // it should not find it because it is instead looking for a DOTENV_PRIVATE_KEY_CI

        // matching ci key is set - inverse of the ci public key in the secrets.ci.txt file
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY_CI", "<privateKeyCi>");
        string? result2 = SmartDotenvPrivateKey(filepath);
        result2.ShouldBe("<privateKeyCi>");

        // an additional random key is set but still with the dotenv private key schema
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY_PRODUCTION", "<privateKeyProduction>");
        string? result3 = SmartDotenvPrivateKey(filepath);
        result3.ShouldBe("<privateKeyCi>"); // it should still find the CI one first
    }
}