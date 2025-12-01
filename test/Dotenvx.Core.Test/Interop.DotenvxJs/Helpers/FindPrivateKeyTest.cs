using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/find-private-key.test.js</remarks>
public class FindPrivateKeyTest : EnvironmentTestBase
{
    /// <remarks>Refer to "#findPrivateKey"</remarks>
    [Fact]
    public void TestFindPrivateKeyReturnsPrivateKey()
    {
        const string envFilePath = "tests/monorepo/apps/encrypted/.env";
        string? privateKey = FindPrivateKey(envFilePath);

        privateKey.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    /// <remarks>Refer to "#findPrivateKey non-standard .env name (secrets.txt)"</remarks>
    [Fact]
    public void TestFindPrivateKeyNonStandardEnvNameSecretsTxtReturnsPrivateKey()
    {
        const string envFilePath = "tests/monorepo/apps/encrypted/secrets.txt";
        string? privateKey = FindPrivateKey(envFilePath);

        privateKey.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    /// <remarks>Refer to "#findPrivateKey non-standard .env name with no matching private key (secrets.ci.txt)"</remarks>
    [Fact]
    public void TestFindPrivateKeyNonStandardEnvNameSecretsCiTxtReturnsNull()
    {
        const string envFilePath = "tests/monorepo/apps/encrypted/secrets.ci.txt";
        string? privateKey = FindPrivateKey(envFilePath);

        privateKey.ShouldBeNull();
    }
}