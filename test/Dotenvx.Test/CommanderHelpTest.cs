using Dotenvx.Help;

using Shouldly;

namespace Dotenvx.Test;

/// <remarks>Not from a JS test - commander's Help.wrap (bundled in @dotenvx/tooling); byte parity of whole help texts is HelpExactParityTest.</remarks>
public class CommanderHelpTest
{
    [Fact]
    public void WrapBreaksAtWhitespaceAndIndentsContinuationLines()
    {
        string text = "-k, --key <keys...>".PadRight(30) + "keys(s) to decrypt (default: all keys in file) and then some more words";

        CommanderHelp.Wrap(text, 78, 30).ShouldBe(
            "-k, --key <keys...>           keys(s) to decrypt (default: all keys in file)\n" +
            "                              and then some more words");
    }

    [Fact]
    public void WrapLeavesTextAloneWhenTheColumnIsNarrowerThan40()
    {
        CommanderHelp.Wrap("a b c d e f g h", 50, 20).ShouldBe("a b c d e f g h");
    }

    [Fact]
    public void WrapLeavesPreformattedTextAlone()
    {
        CommanderHelp.Wrap("first line\n  indented second line", 80, 0).ShouldBe("first line\n  indented second line");
    }

    [Fact]
    public void EveryUpstreamCommandHasASpec()
    {
        CommanderHelp.Find(["run"]).ShouldNotBeNull();
        CommanderHelp.Find(["ext", "scan"]).ShouldNotBeNull();
        CommanderHelp.Find(["rotate"]).ShouldBeNull();
    }
}
