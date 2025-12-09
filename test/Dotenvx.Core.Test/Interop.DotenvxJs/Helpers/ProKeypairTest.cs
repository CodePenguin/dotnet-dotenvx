using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/proKeypair.test.js</remarks>
public class ProKeypairTest
{
    /// <remarks>Refer to "#proKeypair"</remarks>
    [Fact]
    public void TestProKeypair()
    {
        Dictionary<string, string?> keypairs = new ProKeyPair("tests/monorepo/apps/app1/.env").Run();

        keypairs.ShouldBe(
            new Dictionary<string, string?> { { "DOTENV_PUBLIC_KEY", null }, { "DOTENV_PRIVATE_KEY", null } });
    }

    /// <remarks>Refer to "#proKeypair when childProcess fails"</remarks>
    [Fact]
    public void TestProKeypairWhenChildProcessFails()
    {
        // In C#, since no childProcess, it always returns nulls
        Dictionary<string, string?> keypairs = new ProKeyPair("tests/monorepo/apps/app1/.env").Run();

        keypairs.ShouldBe(
            new Dictionary<string, string?> { { "DOTENV_PUBLIC_KEY", null }, { "DOTENV_PRIVATE_KEY", null } });
    }
}