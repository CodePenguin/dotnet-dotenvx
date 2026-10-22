using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/truncate.test.js</remarks>
public class TruncateTest
{
    /// <remarks>Refer to "#truncate"</remarks>
    [Fact]
    public void TestTruncate()
    {
        string privateKey = "2c93601cba85b3b2474817897826ebef977415c097f0bf57dcbaa3056e5d64d0";

        string result = Truncate(privateKey);

        result.ShouldBe("2c93601…");
    }

    /// <remarks>Refer to "#truncate - 11 characters"</remarks>
    [Fact]
    public void TestTruncate11Characters()
    {
        string privateKey = "dxo_123456789";

        string result = Truncate(privateKey, 11);

        result.ShouldBe("dxo_1234567…");
    }

    /// <remarks>Refer to "#truncate - null privateKey"</remarks>
    [Fact]
    public void TestTruncateNullPrivateKey()
    {
        string? privateKey = null;

        string result = Truncate(privateKey);

        result.ShouldBe("");
    }
}
