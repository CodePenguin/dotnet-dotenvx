using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Not from a JS test: expected values come from Node's String.prototype.toUpperCase / toLowerCase.</remarks>
public class JsCaseTest
{
    [Theory]
    [InlineData("production", "PRODUCTION")]
    [InlineData("straße", "STRASSE")]
    [InlineData("ısı", "ISI")]
    [InlineData("İstanbul", "İSTANBUL")]
    [InlineData("ﬁne", "FINE")]
    [InlineData("ǆ", "Ǆ")]
    [InlineData("café", "CAFÉ")]
    [InlineData("σίσυφος", "ΣΊΣΥΦΟΣ")]
    [InlineData("𝐚", "𝐚")]
    public void ToUpperMatchesJavaScript(string value, string expected)
    {
        JsCase.ToUpper(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("MiXeD", "mixed")]
    [InlineData("İstanbul", "i̇stanbul")]
    [InlineData("ΟΔΟΣ", "οδος")]
    [InlineData("Σ", "σ")]
    [InlineData("ΑΣ Σ", "ας σ")]
    [InlineData("ISTANBUL", "istanbul")]
    public void ToLowerMatchesJavaScript(string value, string expected)
    {
        JsCase.ToLower(value).ShouldBe(expected);
    }
}
