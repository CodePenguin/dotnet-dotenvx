using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>Reference source: @dotenvx/primitives scan() (no readable source; verified via live calls)</remarks>
public class ParseTest
{
    /// <remarks>Not from a JS test - found 2026-10-02 via a get parity test: primitives unescapes an escaped single quote inside a single-quoted value, and nothing else.</remarks>
    [Fact]
    public void EscapedSingleQuoteIsUnescapedOnlyInSingleQuotedValues()
    {
        string src = "A='x\\'y'\nB=\"x\\\"y\"\nC=`x\\`y`\n";

        Dictionary<string, object> parsed = ScanDictionary.Of(src);

        parsed["A"].ShouldBe("x'y");
        parsed["B"].ShouldBe("x\\\"y");
        parsed["C"].ShouldBe("x\\`y");
    }
}
