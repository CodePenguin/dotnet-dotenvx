using Dotenvx.Interop.EnquirerJs;

using Shouldly;

namespace Dotenvx.Ports.Test;

/// <remarks>Not from a JS test - class-level checks of the ansi-colors port used by the enquirer port (`style`, `hasColor`, `unstyle`).</remarks>
[Collection("Style.Enabled")]
public class EnquirerStyleTest : IDisposable
{
    private readonly bool _enabled = Style.Enabled;

    public EnquirerStyleTest()
    {
        Style.Enabled = true;
    }

    public void Dispose()
    {
        Style.Enabled = _enabled;
    }

    [Fact]
    public void WrapsTextInTheOpenAndCloseCodes()
    {
        Style.Cyan.Apply("x").ShouldBe("\u001b[36mx\u001b[39m");
        Style.Bold.Apply("x").ShouldBe("\u001b[1mx\u001b[22m");
    }

    [Fact]
    public void EmptyAndNullStayEmpty()
    {
        Style.Cyan.Apply("").ShouldBe("");
        Style.Cyan.Apply(null).ShouldBe("");
    }

    [Fact]
    public void DisabledStylesReturnTheInput()
    {
        Style.Enabled = false;

        Style.Cyan.Apply("x").ShouldBe("x");
    }

    [Fact]
    public void ChainedStylesWrapInTheOrderTheyWereChained()
    {
        Style.Cyan.Underline.Apply("x").ShouldBe("\u001b[36m\u001b[4mx\u001b[24m\u001b[39m");
    }

    [Fact]
    public void NewlinesReopenTheStyleAfterEachBreak()
    {
        Style.Cyan.Apply("a\nb").ShouldBe("\u001b[36ma\u001b[39m\n\u001b[36mb\u001b[39m");
        Style.Cyan.Apply("a\r\nb").ShouldBe("\u001b[36ma\u001b[39m\r\n\u001b[36mb\u001b[39m");
    }

    [Fact]
    public void EmbeddedCloseCodesAreReopened()
    {
        Style.Cyan.Apply("p\u001b[39mq").ShouldBe("\u001b[36mp\u001b[39m\u001b[36mq\u001b[39m");
    }

    [Fact]
    public void HasColorAndUnstyle()
    {
        string styled = Style.Cyan.Apply("x");

        Style.HasColor(styled).ShouldBeTrue();
        Style.HasColor("x").ShouldBeFalse();
        Style.HasColor(null).ShouldBeFalse();
        Style.Unstyle(styled).ShouldBe("x");
        Style.StripAnsi(styled).ShouldBe("x");
    }

    [Fact]
    public void InverseSwapsOnlyCyan()
    {
        Style.Inverse(Style.Cyan).Apply("x").ShouldBe("\u001b[46m\u001b[30mx\u001b[39m\u001b[49m");
        Style.Inverse(Style.Green).ShouldBeSameAs(Style.Green);
    }
}
