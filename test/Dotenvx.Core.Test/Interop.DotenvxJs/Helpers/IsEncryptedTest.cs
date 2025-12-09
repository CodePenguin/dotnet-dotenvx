using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/isEncrypted.test.js</remarks>
public class IsEncryptedTest
{
    /// <remarks>Refer to "#isEncrypted"</remarks>
    [Fact]
    public void TestIsEncrypted()
    {
        bool result = IsEncrypted("encrypted:1234");

        result.ShouldBe(true);
    }

    /// <remarks>Refer to "#isEncrypted real world"</remarks>
    [Fact]
    public void TestIsEncryptedRealWorld()
    {
        bool result =
            IsEncrypted(
                "encrypted:BHqJQhgpCHY4My3PQ1UE3vSTyfqM/wNaISWSVQgi8eBQze4X7AMkcl0tg4skow5vI7Akhm0UXV43+FeYOvxcXifjjKbHZeXp+hFxKk5zu/N3tB95DiCpXSLA2bbcyeTNLAfTZgXa");

        result.ShouldBe(true);
    }

    /// <remarks>Refer to "#isEncrypted not encrypted"</remarks>
    [Fact]
    public void TestIsEncryptedNotEncrypted()
    {
        bool result = IsEncrypted("1234");

        result.ShouldBe(false);
    }

    /// <remarks>Refer to "#isEncrypted passes null"</remarks>
    [Fact]
    public void TestIsEncryptedPassesNull()
    {
        bool result = IsEncrypted(null);

        result.ShouldBe(false);
    }
}