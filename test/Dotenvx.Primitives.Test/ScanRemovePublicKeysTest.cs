using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>
/// Not from a JS test - every expectation below was read off the live @dotenvx/primitives 3.0.2
/// (scan/remove/publickeys) on 2026-10-02; PrimitivesOracleParityTest keeps them honest.
/// </remarks>
public class ScanRemovePublicKeysTest
{
    private static readonly string Nbsp = ((char)0xA0).ToString();
    private static readonly string LineSep = ((char)0x2028).ToString();

    [Fact]
    public void ScanTreatsNbspAsWhitespaceAroundKeyAndValue()
    {
        List<ScanEntry> entries = Scan(Nbsp + "A=" + Nbsp + "1" + Nbsp + "\nB=\"x\"" + Nbsp + "# c");

        entries.Select(e => (e.Key, e.Value)).ShouldBe([("A", "1"), ("B", "x")]);
    }

    [Fact]
    public void ScanTreatsLoneCarriageReturnAsLineBreak()
    {
        List<ScanEntry> entries = Scan("A=1\rB=2\r\rC=3");

        entries.Select(e => (e.Key, e.Value)).ShouldBe([("A", "1"), ("B", "2"), ("C", "3")]);
    }

    [Fact]
    public void ScanDoesNotSplitValueAtLineSeparator()
    {
        List<ScanEntry> entries = Scan("A=1" + LineSep + "B=2");

        entries.Count.ShouldBe(1);
        entries[0].Value.ShouldBe("1" + LineSep + "B=2");
    }

    [Fact]
    public void RemoveDropsOnlyTheKeyLine()
    {
        Remove("A=1\nB=2\nC=3\n", "B").ShouldBe("A=1\nC=3\n");
    }

    [Fact]
    public void RemoveConsumesMultilineQuotedValueAndKeepsCrlf()
    {
        Remove("A=1\r\nB=\"x\r\ny\"\r\nC=3\r\n", "B").ShouldBe("A=1\r\nC=3\r\n");
    }

    [Fact]
    public void RemovePreservesBlankLines()
    {
        Remove("A=1\n\nB=2\n", "A").ShouldBe("\nB=2\n");
    }

    [Fact]
    public void RemoveUnterminatedQuoteConsumesRestOfFile()
    {
        Remove("A=1\nB=\"open\nC=3", "B").ShouldBe("A=1");
    }

    [Fact]
    public void RemoveLastKeyYieldsEmptyString()
    {
        Remove("A=1\n", "A").ShouldBe("");
    }

    [Fact]
    public void RemoveMissingKeyReturnsSourceUnchanged()
    {
        Remove("A=1\n", "Z").ShouldBe("A=1\n");
    }

    [Fact]
    public void PublicKeysKeepsLastValuePerKeyInFirstAppearanceOrder()
    {
        PublicKeys("DOTENV_PUBLIC_KEY=a\nDOTENV_PUBLIC_KEY_X=b\nDOTENV_PUBLIC_KEY=c\nA=1").ShouldBe(["c", "b"]);
    }

    [Fact]
    public void UpsertReplacesEveryOccurrenceKeepingEachQuoteStyle()
    {
        Upsert("A=1\nA=2\n", "A", "9").ShouldBe("A=9\nA=9\n");
        Upsert("A=\"q\"\nA='r'\n", "A", "z").ShouldBe("A=\"z\"\nA='z'\n");
    }

    [Fact]
    public void UpsertAppendsAbsentKeyWithLeadingNewlineWhenSourceLacksOne()
    {
        Upsert("A=1", "B", "x").ShouldBe("A=1\nB=\"x\"");
    }
}
