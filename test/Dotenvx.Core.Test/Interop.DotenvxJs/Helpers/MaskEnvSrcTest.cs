using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/maskEnvSrc.js (no upstream unit test file). Expected values were read off
/// the real helper: every occurrence of a duplicate key is masked with its own value, and double-quoted escapes are
/// expanded before masking (so the masked length is that of the real text).
/// </remarks>
public class MaskEnvSrcTest
{
    /// <remarks>Not from a JS test - found by comparing this port with maskEnvSrc.js line by line.</remarks>
    [Fact]
    public void TestMasksEveryOccurrenceAndExpandsEscapesBeforeMasking()
    {
        string src = "A=1234567890\nA=ab\nB=\"x\\ny\\nz\"\nC=plain # c\n";

        MaskEnvSrc(src, 3).ShouldBe("A=123*******\nA=a*\nB=\"x\ny**\"\nC=pla** # c\n");
    }

    /// <remarks>Not from a JS test - the default shows 6 characters.</remarks>
    [Fact]
    public void TestDefaultShowsSixCharacters()
    {
        MaskEnvSrc("A=secretvalue\n").ShouldBe("A=secret*****\n");
    }
}
