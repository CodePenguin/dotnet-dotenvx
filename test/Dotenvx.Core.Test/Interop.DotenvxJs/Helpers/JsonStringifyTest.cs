using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Not from a JS test - expected strings are what Node's `JSON.stringify` prints, verified live.</remarks>
public class JsonStringifyTest
{
    [Theory]
    [InlineData("a&b<c>d+e'f", "\"a&b<c>d+e'f\"")]
    [InlineData("héllo😀", "\"héllo😀\"")]
    [InlineData("\u007F\u2028\u00A0\uFEFF", "\"\u007F\u2028\u00A0\uFEFF\"")]
    [InlineData("q\\\"b", "\"q\\\\\\\"b\"")]
    [InlineData("a\\u00e9", "\"a\\\\u00e9\"")]
    public void StringsAreEscapedLikeJsonStringify(string value, string expected)
    {
        JsonStringify(value).ShouldBe(expected);
    }

    [Fact]
    public void ControlCharactersUseLowercaseHexAndShortForms()
    {
        JsonStringify("\u0001\u001f\t\n").ShouldBe("\"\\u0001\\u001f\\t\\n\"");
    }

    [Fact]
    public void IndentedOutputUsesLineFeedOnly()
    {
        JsonStringify(new Dictionary<string, string> { ["A"] = "b" }, true).ShouldBe("{\n  \"A\": \"b\"\n}");
    }
}
