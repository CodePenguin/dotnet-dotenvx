using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

public class SmartDotenvPublicKeyTest : EnvironmentTestBase
{
    private const string Filepath = ".env";

    [Fact]
    public void SmartDotenvPublicKey_ReturnsNull()
    {
        string? result = DotenvxHelpers.SmartDotenvPublicKey(Filepath);

        result.ShouldBeNull();
    }

    [Fact]
    public void SmartDotenvPublicKey_WhenProcessEnvDotenvPublicKeyIsSet_ReturnsPublicKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "<publicKey>");

        string? result = DotenvxHelpers.SmartDotenvPublicKey(Filepath);

        result.ShouldBe("<publicKey>");
    }

    [Fact]
    public void SmartDotenvPublicKey_WhenProcessEnvDotenvPublicKeyIsSetButEmpty_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "");

        string? result = DotenvxHelpers.SmartDotenvPublicKey(Filepath);

        result.ShouldBeNull();
    }

    [Fact]
    public void SmartDotenvPublicKey_WhenEnvKeysPresent_ReturnsPublicKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/.env";

        string? result = DotenvxHelpers.SmartDotenvPublicKey(filepath);

        result.ShouldBe("03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba");
    }
}