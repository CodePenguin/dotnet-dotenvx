using Dotenvx.Core.Interop.DotenvxJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx;

public class ParseTest
{
    /// <summary>
    /// Equivalent tests from https://github.com/dotenvx/dotenvx/blob/main/tests/lib/parse.test.js
    /// </summary>
    [Fact]
    public void TestDotenvxParseTests()
    {
        var src = File.ReadAllText("./.env");
        var parsed = Main.Parse(src);
        parsed.ShouldNotBeNull("should return an object");
        parsed["BASIC"].ShouldBe("basic", "sets basic environment variable");
        parsed["AFTER_LINE"].ShouldBe("after_line", "reads after a skipped line");
        parsed["EMPTY"].ShouldBe("", "defaults empty values to empty string");
        parsed["EMPTY_SINGLE_QUOTES"].ShouldBe("", "defaults empty values to empty string");
        parsed["EMPTY_DOUBLE_QUOTES"].ShouldBe("", "defaults empty values to empty string");
        parsed["EMPTY_BACKTICKS"].ShouldBe("", "defaults empty values to empty string");
        parsed["SINGLE_QUOTES"].ShouldBe("single_quotes", "escapes single quoted values");
        parsed["SINGLE_QUOTES_SPACED"].ShouldBe("    single quotes    ", "respects surrounding spaces in single quotes");
        parsed["DOUBLE_QUOTES"].ShouldBe("double_quotes", "escapes double quoted values");
        parsed["DOUBLE_QUOTES_SPACED"].ShouldBe("    double quotes    ", "respects surrounding spaces in double quotes");
        parsed["DOUBLE_QUOTES_INSIDE_SINGLE"].ShouldBe("double \"quotes\" work inside single quotes", "respects double quotes inside single quotes");
        parsed["DOUBLE_QUOTES_WITH_NO_SPACE_BRACKET"].ShouldBe("{ port: }", "respects spacing for badly formed brackets");
        parsed["SINGLE_QUOTES_INSIDE_DOUBLE"].ShouldBe("single 'quotes' work inside double quotes", "respects single quotes inside double quotes");
        parsed["BACKTICKS_INSIDE_SINGLE"].ShouldBe("`backticks` work inside single quotes", "respects backticks inside single quotes");
        parsed["BACKTICKS_INSIDE_DOUBLE"].ShouldBe("`backticks` work inside double quotes", "respects backticks inside double quotes");
        parsed["BACKTICKS"].ShouldBe("backticks");
        parsed["BACKTICKS_SPACED"].ShouldBe("    backticks    ");
        parsed["DOUBLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("double \"quotes\" work inside backticks", "respects double quotes inside backticks");
        parsed["SINGLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("single 'quotes' work inside backticks", "respects single quotes inside backticks");
        parsed["DOUBLE_AND_SINGLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("double \"quotes\" and single 'quotes' work inside backticks", "respects single quotes inside backticks");
        parsed["EXPAND_NEWLINES"].ShouldBe("expand\nnew\nlines", "expands newlines but only if double quoted");
        parsed["DONT_EXPAND_UNQUOTED"].ShouldBe("dontexpand\\nnewlines", "does not expand newlines if unquoted");
        parsed["DONT_EXPAND_SQUOTED"].ShouldBe("dontexpand\\nnewlines", "does not expand newlines if single quoted");
        parsed.ContainsKey("COMMENTS").ShouldBeFalse("ignores commented lines");
        parsed["INLINE_COMMENTS"].ShouldBe("inline comments", "ignores inline comments");
        parsed["INLINE_COMMENTS_SINGLE_QUOTES"].ShouldBe("inline comments outside of #singlequotes", "ignores inline comments and respects # character inside of single quotes");
        parsed["INLINE_COMMENTS_DOUBLE_QUOTES"].ShouldBe("inline comments outside of #doublequotes", "ignores inline comments and respects # character inside of double quotes");
        parsed["INLINE_COMMENTS_BACKTICKS"].ShouldBe("inline comments outside of #backticks", "ignores inline comments and respects # character inside of backticks");
        parsed["INLINE_COMMENTS_SPACE"].ShouldBe("inline comments start with a", "treats # character as start of comment");
        parsed["EQUAL_SIGNS"].ShouldBe("equals==", "respects equals signs in values");
        parsed["RETAIN_INNER_QUOTES"].ShouldBe("{\"foo\": \"bar\"}", "retains inner quotes");
        parsed["RETAIN_INNER_QUOTES_AS_STRING"].ShouldBe("{\"foo\": \"bar\"}", "retains inner quotes");
        parsed["RETAIN_INNER_QUOTES_AS_BACKTICKS"].ShouldBe("{\"foo\": \"bar's\"}", "retains inner quotes");
        parsed["TRIM_SPACE_FROM_UNQUOTED"].ShouldBe("some spaced out string", "trims spaces from unquoted values");
        parsed["USERNAME"].ShouldBe("therealnerdybeast@example.tld", "parses email addresses completely");
        parsed["SPACED_KEY"].ShouldBe("parsed", "parses keys and values surrounded by spaces");
        
        var expectedPayload = new Dictionary<string, string>
        {
            { "SERVER", "localhost" },
            { "PASSWORD", "password"},
            { "DB", "tests" }
        };
        
        var rPayload = Main.Parse("SERVER=localhost\rPASSWORD=password\rDB=tests\r");
        rPayload.ShouldBe(expectedPayload, @"can parse (\r) line endings");

        var nPayload = Main.Parse("SERVER=localhost\nPASSWORD=password\nDB=tests\n");
        nPayload.ShouldBe(expectedPayload, @"can parse (\n) line endings");

        var rnPayload = Main.Parse("SERVER=localhost\r\nPASSWORD=password\r\nDB=tests\r\n");
        rnPayload.ShouldBe(expectedPayload, @"can parse (\n) line endings");
    }
}