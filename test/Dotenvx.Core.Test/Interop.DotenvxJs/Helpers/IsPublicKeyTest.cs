using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/isPublicKey.test.js</remarks>
public class IsPublicKeyTest
{
    /// <remarks>Refer to "#isPublicKey not encrypted but DOTENV_PUBLIC_KEY"</remarks>
    [Fact]
    public void TestIsPublicKeyNotEncryptedButDotenvPublicKeyReturnsTrue()
    {
        bool result = IsPublicKey("DOTENV_PUBLIC_KEY", "1234");

        result.ShouldBeTrue();
    }

    /// <remarks>Refer to "#isPublicKey not encrypted but DOTENV_PUBLIC_KEY_PRODUCTION"</remarks>
    [Fact]
    public void TestIsPublicKeyNotEncryptedButDotenvPublicKeyProductionReturnsTrue()
    {
        bool result = IsPublicKey("DOTENV_PUBLIC_KEY_PRODUCTION", "1234");

        result.ShouldBeTrue();
    }
}
