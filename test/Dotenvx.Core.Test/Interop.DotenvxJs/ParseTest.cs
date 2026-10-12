using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs;

/// <remarks>Reference source: dotenvx/tests/lib/parse.test.js</remarks>
public class ParseTest : EnvironmentTestBase
{
    private readonly string _src = Fsx.ReadFileX("tests/.env");

    public ParseTest()
    {
        ClearEnvironmentExceptPath();
    }

    /// <remarks>Refer to "should return an object"</remarks>
    [Fact]
    public void TestShouldReturnAnObject()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed.ShouldNotBeNull("should return an object");
    }

    /// <remarks>Refer to "sets basic environment variable"</remarks>
    [Fact]
    public void TestSetsBasicEnvironmentVariable()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BASIC"].ShouldBe("basic");
    }

    /// <remarks>Refer to "reads after a skipped line"</remarks>
    [Fact]
    public void TestReadsAfterASkippedLine()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["AFTER_LINE"].ShouldBe("after_line");
    }

    /// <remarks>Refer to "defaults empty values to empty string"</remarks>
    [Fact]
    public void TestDefaultsEmptyValuesToEmptyString()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["EMPTY"].ShouldBe("");
        parsed["EMPTY_SINGLE_QUOTES"].ShouldBe("");
        parsed["EMPTY_DOUBLE_QUOTES"].ShouldBe("");
        parsed["EMPTY_BACKTICKS"].ShouldBe("");
    }

    /// <remarks>Refer to "escapes single quoted values"</remarks>
    [Fact]
    public void TestEscapesSingleQuotedValues()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES"].ShouldBe("single_quotes");
    }

    /// <remarks>Refer to "respects surrounding spaces in single quotes"</remarks>
    [Fact]
    public void TestRespectsSurroundingSpacesInSingleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES_SPACED"].ShouldBe("    single quotes    ");
    }

    /// <remarks>Refer to "escapes double quoted values"</remarks>
    [Fact]
    public void TestEscapesDoubleQuotedValues()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES"].ShouldBe("double_quotes");
    }

    /// <remarks>Refer to "respects surrounding spaces in double quotes"</remarks>
    [Fact]
    public void TestRespectsSurroundingSpacesInDoubleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_SPACED"].ShouldBe("    double quotes    ");
    }

    /// <remarks>Refer to "respects double quotes inside single quotes"</remarks>
    [Fact]
    public void TestRespectsDoubleQuotesInsideSingleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_INSIDE_SINGLE"].ShouldBe("double \"quotes\" work inside single quotes");
    }

    /// <remarks>Refer to "respects spacing for badly formed brackets"</remarks>
    [Fact]
    public void TestRespectsSpacingForBadlyFormedBrackets()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_WITH_NO_SPACE_BRACKET"].ShouldBe("{ port: }");
    }

    /// <remarks>Refer to "respects single quotes inside double quotes"</remarks>
    [Fact]
    public void TestRespectsSingleQuotesInsideDoubleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES_INSIDE_DOUBLE"].ShouldBe("single 'quotes' work inside double quotes");
    }

    /// <remarks>Refer to "respects backticks inside single quotes"</remarks>
    [Fact]
    public void TestRespectsBackticksInsideSingleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS_INSIDE_SINGLE"].ShouldBe("`backticks` work inside single quotes");
    }

    [Fact]
    public void TestRespectsBackticksInsideDoubleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS_INSIDE_DOUBLE"].ShouldBe("`backticks` work inside double quotes");
    }

    [Fact]
    public void TestParsesBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS"].ShouldBe("backticks");
    }

    [Fact]
    public void TestParsesBackticksSpaced()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["BACKTICKS_SPACED"].ShouldBe("    backticks    ");
    }

    [Fact]
    public void TestRespectsDoubleQuotesInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("double \"quotes\" work inside backticks");
    }

    [Fact]
    public void TestRespectsSingleQuotesInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SINGLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("single 'quotes' work inside backticks");
    }

    [Fact]
    public void TestRespectsDoubleAndSingleQuotesInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DOUBLE_AND_SINGLE_QUOTES_INSIDE_BACKTICKS"]
            .ShouldBe("double \"quotes\" and single 'quotes' work inside backticks");
    }

    [Fact]
    public void TestExpandsNewlinesButOnlyIfDoubleQuoted()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["EXPAND_NEWLINES"].ShouldBe("expand\nnew\nlines");
    }

    [Fact]
    public void TestDoesNotExpandNewlinesIfUnquoted()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DONT_EXPAND_UNQUOTED"].ShouldBe("dontexpand\\nnewlines");
    }

    [Fact]
    public void TestDoesNotExpandNewlinesIfSingleQuoted()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["DONT_EXPAND_SQUOTED"].ShouldBe("dontexpand\\nnewlines");
    }

    [Fact]
    public void TestIgnoresCommentedLines()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed.ContainsKey("COMMENTS").ShouldBeFalse();
    }

    [Fact]
    public void TestIgnoresInlineComments()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS"].ShouldBe("inline comments");
    }

    [Fact]
    public void TestIgnoresInlineCommentsAndRespectsHashCharacterInsideSingleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_SINGLE_QUOTES"].ShouldBe("inline comments outside of #singlequotes");
    }

    [Fact]
    public void TestIgnoresInlineCommentsAndRespectsHashCharacterInsideDoubleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_DOUBLE_QUOTES"].ShouldBe("inline comments outside of #doublequotes");
    }

    [Fact]
    public void TestIgnoresInlineCommentsAndRespectsHashCharacterInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_BACKTICKS"].ShouldBe("inline comments outside of #backticks");
    }

    [Fact]
    public void TestTreatsHashCharacterAsStartOfComment()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["INLINE_COMMENTS_SPACE"].ShouldBe("inline comments start with a");
    }

    [Fact]
    public void TestRespectsEqualsSignsInValues()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["EQUAL_SIGNS"].ShouldBe("equals==");
    }

    [Fact]
    public void TestRetainsInnerQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["RETAIN_INNER_QUOTES"].ShouldBe("{\"foo\": \"bar\"}");
        parsed["RETAIN_INNER_QUOTES_AS_STRING"].ShouldBe("{\"foo\": \"bar\"}");
        parsed["RETAIN_INNER_QUOTES_AS_BACKTICKS"].ShouldBe("{\"foo\": \"bar's\"}");
    }

    [Fact]
    public void TestTrimsSpacesFromUnquotedValues()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["TRIM_SPACE_FROM_UNQUOTED"].ShouldBe("some spaced out string");
    }

    [Fact]
    public void TestParsesEmailAddressesCompletely()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["USERNAME"].ShouldBe("therealnerdybeast@example.tld");
    }

    [Fact]
    public void TestParsesKeysAndValuesSurroundedBySpaces()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SPACED_KEY"].ShouldBe("parsed");
    }

    [Fact]
    public void TestCanParseCarriageReturnLineEndings()
    {
        Dictionary<string, string> expectedPayload = new()
        {
            { "SERVER", "localhost" }, { "PASSWORD", "password" }, { "DB", "tests" }
        };

        IReadOnlyDictionary<string, string> rPayload = Main.Parse("SERVER=localhost\rPASSWORD=password\rDB=tests\r");
        rPayload.ShouldBe(expectedPayload);
    }

    [Fact]
    public void TestCanParseNewlineLineEndings()
    {
        Dictionary<string, string> expectedPayload = new()
        {
            { "SERVER", "localhost" }, { "PASSWORD", "password" }, { "DB", "tests" }
        };

        IReadOnlyDictionary<string, string> nPayload = Main.Parse("SERVER=localhost\nPASSWORD=password\nDB=tests\n");
        nPayload.ShouldBe(expectedPayload);
    }

    [Fact]
    public void TestCanParseCarriageReturnNewlineLineEndings()
    {
        Dictionary<string, string> expectedPayload = new()
        {
            { "SERVER", "localhost" }, { "PASSWORD", "password" }, { "DB", "tests" }
        };

        IReadOnlyDictionary<string, string> rnPayload =
            Main.Parse("SERVER=localhost\r\nPASSWORD=password\r\nDB=tests\r\n");
        rnPayload.ShouldBe(expectedPayload);
    }
}