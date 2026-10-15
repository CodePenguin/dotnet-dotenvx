using Dotenvx.Interop.EnquirerJs;

using Shouldly;

namespace Dotenvx.Ports.Test;

/// <remarks>
/// Not from a JS test - class-level checks of the enquirer port's escape-sequence helpers (enquirer lib/ansi.js and ansi-colors).
/// The prompts themselves are compared byte for byte against the real package by EnquirerParityTest.
/// </remarks>
public class EnquirerAnsiTest
{
    private const string Esc = "\u001b[";

    [Fact]
    public void CursorMovesUseTheDocumentedSequences()
    {
        Ansi.Up(2).ShouldBe(Esc + "2A");
        Ansi.Down().ShouldBe(Esc + "1B");
        Ansi.Right(3).ShouldBe(Esc + "3C");
        Ansi.Left(4).ShouldBe(Esc + "4D");
        Ansi.Hide.ShouldBe(Esc + "?25l");
        Ansi.Show.ShouldBe(Esc + "?25h");
        Ansi.Line.ShouldBe(Esc + "2K");
    }

    [Fact]
    public void ZeroCountMovesAreEmpty()
    {
        Ansi.Up(0).ShouldBe("");
        Ansi.Down(0).ShouldBe("");
        Ansi.Right(0).ShouldBe("");
        Ansi.Left(0).ShouldBe("");
    }

    [Fact]
    public void ToIsOneBasedOnTheWire()
    {
        Ansi.To(3).ShouldBe(Esc + "4G");
        Ansi.To(3, 4).ShouldBe(Esc + "5;4H");
    }

    [Fact]
    public void MoveCombinesHorizontalAndVertical()
    {
        Ansi.Move(-2, 3).ShouldBe(Esc + "2D" + Esc + "3B");
        Ansi.Move(2, -1).ShouldBe(Esc + "2C" + Esc + "1A");
        Ansi.Move().ShouldBe("");
    }

    [Fact]
    public void PrevLineRepeats()
    {
        Ansi.PrevLine(2).ShouldBe(Esc + "F" + Esc + "F");
    }

    [Fact]
    public void StrLenCountsNonAsciiAsTwo()
    {
        Ansi.StrLen("abc").ShouldBe(3);
        Ansi.StrLen("a漢").ShouldBe(3);
        Ansi.StrLen("").ShouldBe(0);
    }

    [Fact]
    public void ClearWithoutColumnsErasesOneLine()
    {
        Ansi.Clear("anything", null).ShouldBe(Esc + "2K" + Esc + "1G");
        Ansi.Clear("anything", 0).ShouldBe(Esc + "2K" + Esc + "1G");
    }

    [Fact]
    public void ClearErasesEveryWrappedRow()
    {
        // "ab" fits one row; "cdefg" wraps over two rows of four columns
        Ansi.Clear("ab\ncdefg", 4)
            .ShouldBe(string.Concat(Enumerable.Repeat(Esc + "2K" + Esc + "F", 2)) + Esc + "2K" + Esc + "1G");
    }

    [Fact]
    public void ClearIgnoresStyleCodesWhenMeasuring()
    {
        Ansi.Clear(Esc + "36mabcd" + Esc + "39m", 4).ShouldBe(Esc + "2K" + Esc + "1G");
    }

    [Fact]
    public void RestoreReturnsNullWhenThereIsNothingToRestore()
    {
        Ansi.Restore("", 0, "", "", "> ", 0, "").ShouldBeNull();
    }
}
