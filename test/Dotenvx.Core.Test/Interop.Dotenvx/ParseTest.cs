using Dotenvx.Core.Interop.DotenvxJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx;

public class ParseTest
{
    private readonly string _src = File.ReadAllText("tests/.env");

    /// <summary>
    ///     Equivalent tests from https://github.com/dotenvx/dotenvx/blob/main/tests/lib/parse.test.js
    /// </summary>
    [Fact]
    public void ShouldReturnAnObject()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed.ShouldNotBeNull("should return an object");
    }

    [Fact]
    public void SetsBasicEnvironmentVariable()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void ReadsAfterASkippedLine()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["AFTER_LINE"].ShouldBe("after_line");
    }

    [Fact]
    public void DefaultsEmptyValuesToEmptyString()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["EMPTY"].ShouldBe("");
        parsed["EMPTY_SINGLE_QUOTES"].ShouldBe("");
        parsed["EMPTY_DOUBLE_QUOTES"].ShouldBe("");
        parsed["EMPTY_BACKTICKS"].ShouldBe("");
    }

    [Fact]
    public void EscapesSingleQuotedValues()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES"].ShouldBe("single_quotes");
    }

    [Fact]
    public void RespectsSurroundingSpacesInSingleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES_SPACED"].ShouldBe("    single quotes    ");
    }

    [Fact]
    public void EscapesDoubleQuotedValues()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES"].ShouldBe("double_quotes");
    }

    [Fact]
    public void RespectsSurroundingSpacesInDoubleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_SPACED"].ShouldBe("    double quotes    ");
    }

    [Fact]
    public void RespectsDoubleQuotesInsideSingleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_INSIDE_SINGLE"].ShouldBe("double \"quotes\" work inside single quotes");
    }

    [Fact]
    public void RespectsSpacingForBadlyFormedBrackets()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_WITH_NO_SPACE_BRACKET"].ShouldBe("{ port: }");
    }

    [Fact]
    public void RespectsSingleQuotesInsideDoubleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES_INSIDE_DOUBLE"].ShouldBe("single 'quotes' work inside double quotes");
    }

    [Fact]
    public void RespectsBackticksInsideSingleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS_INSIDE_SINGLE"].ShouldBe("`backticks` work inside single quotes");
    }

    [Fact]
    public void RespectsBackticksInsideDoubleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS_INSIDE_DOUBLE"].ShouldBe("`backticks` work inside double quotes");
    }

    [Fact]
    public void ParsesBackticks()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS"].ShouldBe("backticks");
    }

    [Fact]
    public void ParsesBackticksSpaced()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS_SPACED"].ShouldBe("    backticks    ");
    }

    [Fact]
    public void RespectsDoubleQuotesInsideBackticks()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("double \"quotes\" work inside backticks");
    }

    [Fact]
    public void RespectsSingleQuotesInsideBackticks()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("single 'quotes' work inside backticks");
    }

    [Fact]
    public void RespectsDoubleAndSingleQuotesInsideBackticks()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_AND_SINGLE_QUOTES_INSIDE_BACKTICKS"]
            .ShouldBe("double \"quotes\" and single 'quotes' work inside backticks");
    }

    [Fact]
    public void ExpandsNewlinesButOnlyIfDoubleQuoted()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["EXPAND_NEWLINES"].ShouldBe("expand\nnew\nlines");
    }

    [Fact]
    public void DoesNotExpandNewlinesIfUnquoted()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DONT_EXPAND_UNQUOTED"].ShouldBe("dontexpand\\nnewlines");
    }

    [Fact]
    public void DoesNotExpandNewlinesIfSingleQuoted()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DONT_EXPAND_SQUOTED"].ShouldBe("dontexpand\\nnewlines");
    }

    [Fact]
    public void IgnoresCommentedLines()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed.ContainsKey("COMMENTS").ShouldBeFalse();
    }

    [Fact]
    public void IgnoresInlineComments()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS"].ShouldBe("inline comments");
    }

    [Fact]
    public void IgnoresInlineCommentsAndRespectsHashCharacterInsideSingleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_SINGLE_QUOTES"].ShouldBe("inline comments outside of #singlequotes");
    }

    [Fact]
    public void IgnoresInlineCommentsAndRespectsHashCharacterInsideDoubleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_DOUBLE_QUOTES"].ShouldBe("inline comments outside of #doublequotes");
    }

    [Fact]
    public void IgnoresInlineCommentsAndRespectsHashCharacterInsideBackticks()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_BACKTICKS"].ShouldBe("inline comments outside of #backticks");
    }

    [Fact]
    public void TreatsHashCharacterAsStartOfComment()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_SPACE"].ShouldBe("inline comments start with a");
    }

    [Fact]
    public void RespectsEqualsSignsInValues()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["EQUAL_SIGNS"].ShouldBe("equals==");
    }

    [Fact]
    public void RetainsInnerQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["RETAIN_INNER_QUOTES"].ShouldBe("{\"foo\": \"bar\"}");
        parsed["RETAIN_INNER_QUOTES_AS_STRING"].ShouldBe("{\"foo\": \"bar\"}");
        parsed["RETAIN_INNER_QUOTES_AS_BACKTICKS"].ShouldBe("{\"foo\": \"bar's\"}");
    }

    [Fact]
    public void TrimsSpacesFromUnquotedValues()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["TRIM_SPACE_FROM_UNQUOTED"].ShouldBe("some spaced out string");
    }

    [Fact]
    public void ParsesEmailAddressesCompletely()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["USERNAME"].ShouldBe("therealnerdybeast@example.tld");
    }

    [Fact]
    public void ParsesKeysAndValuesSurroundedBySpaces()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SPACED_KEY"].ShouldBe("parsed");
    }

    [Fact]
    public void CanParseCarriageReturnLineEndings()
    {
        Dictionary<string, string> expectedPayload = new()
        {
            { "SERVER", "localhost" }, { "PASSWORD", "password" }, { "DB", "tests" }
        };

        IDictionary<string, string> rPayload = Main.Parse("SERVER=localhost\rPASSWORD=password\rDB=tests\r");
        rPayload.ShouldBe(expectedPayload);
    }

    [Fact]
    public void CanParseNewlineLineEndings()
    {
        Dictionary<string, string> expectedPayload = new()
        {
            { "SERVER", "localhost" }, { "PASSWORD", "password" }, { "DB", "tests" }
        };

        IDictionary<string, string> nPayload = Main.Parse("SERVER=localhost\nPASSWORD=password\nDB=tests\n");
        nPayload.ShouldBe(expectedPayload);
    }

    [Fact]
    public void CanParseCarriageReturnNewlineLineEndings()
    {
        Dictionary<string, string> expectedPayload = new()
        {
            { "SERVER", "localhost" }, { "PASSWORD", "password" }, { "DB", "tests" }
        };

        IDictionary<string, string> rnPayload = Main.Parse("SERVER=localhost\r\nPASSWORD=password\r\nDB=tests\r\n");
        rnPayload.ShouldBe(expectedPayload);
    }
}