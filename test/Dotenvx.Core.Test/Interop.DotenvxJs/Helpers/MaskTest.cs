using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/mask.js (expected outputs live-verified via node)</remarks>
public class MaskTest
{
    [Fact]
    public void MaskWithDefaultShowChar()
    {
        Mask("SuperSecretValue").ShouldBe("SuperS**********");
    }

    [Fact]
    public void MaskWithCustomShowChar()
    {
        Mask("SuperSecretValue", 3).ShouldBe("Sup*************");
    }

    [Fact]
    public void MaskClampsShowCharToHalfLength()
    {
        Mask("Hi", 6).ShouldBe("H*");
    }

    [Fact]
    public void MaskSingleCharStringAlwaysZeroVisible()
    {
        Mask("H", 6).ShouldBe("*");
    }

    [Fact]
    public void MaskEmptyString()
    {
        Mask("", 6).ShouldBe("");
    }

    [Fact]
    public void MaskNonNumericShowCharFallsBackToZeroVisible()
    {
        Mask("abc", "notanumber").ShouldBe("***");
    }

    [Fact]
    public void MaskNegativeShowCharUsesAbsoluteValue()
    {
        Mask("abcdef", -2).ShouldBe("ab****");
    }
}
