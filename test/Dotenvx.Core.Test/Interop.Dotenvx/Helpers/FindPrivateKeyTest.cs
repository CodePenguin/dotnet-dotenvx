using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

public class FindPrivateKeyTest : EnvironmentTestBase
{
    [Fact]
    public void FindPrivateKey_ReturnsPrivateKey()
    {
        const string envFilepath = "tests/monorepo/apps/encrypted/.env";
        string? privateKey = DotenvxHelpers.FindPrivateKey(envFilepath);

        privateKey.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    [Fact]
    public void FindPrivateKey_NonStandardEnvNameSecretsTxt_ReturnsPrivateKey()
    {
        const string envFilepath = "tests/monorepo/apps/encrypted/secrets.txt";
        string? privateKey = DotenvxHelpers.FindPrivateKey(envFilepath);

        privateKey.ShouldBe("ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1");
    }

    [Fact]
    public void FindPrivateKey_NonStandardEnvNameSecretsCiTxt_ReturnsNull()
    {
        const string envFilepath = "tests/monorepo/apps/encrypted/secrets.ci.txt";
        string? privateKey = DotenvxHelpers.FindPrivateKey(envFilepath);

        privateKey.ShouldBeNull();
    }
}