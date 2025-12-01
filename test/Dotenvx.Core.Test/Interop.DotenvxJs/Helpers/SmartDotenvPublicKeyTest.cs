using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/smart-dotenv-public-key.test.js</remarks>
public class SmartDotenvPublicKeyTest : EnvironmentTestBase
{
    private const string FilePath = ".env";

    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyReturnsNull()
    {
        string? result = SmartDotenvPublicKey(FilePath);

        result.ShouldBeNull();
    }

    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyWhenProcessEnvDotenvPublicKeyIsSetReturnsPublicKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "<publicKey>");

        string? result = SmartDotenvPublicKey(FilePath);

        result.ShouldBe("<publicKey>");
    }

    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyWhenProcessEnvDotenvPublicKeyIsSetButEmptyReturnsNull()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "");

        string? result = SmartDotenvPublicKey(FilePath);

        result.ShouldBeNull();
    }

    /// <remarks>Refer to "#smartDotenvPublicKey"</remarks>
    [Fact]
    public void TestSmartDotenvPublicKeyWhenEnvKeysPresentReturnsPublicKey()
    {
        const string filepath = "tests/monorepo/apps/encrypted/.env";

        string? result = SmartDotenvPublicKey(filepath);

        result.ShouldBe("03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba");
    }
}