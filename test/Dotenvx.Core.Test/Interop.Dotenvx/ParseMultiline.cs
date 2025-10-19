using Dotenvx.Core.Interop.DotenvxJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx;

public class ParseMultiline
{
    private readonly string _src = File.ReadAllText("tests/.env.multiline");

    /// <summary>
    ///     Equivalent tests from https://github.com/dotenvx/dotenvx/blob/main/tests/lib/parse-multiline.test.js
    /// </summary>
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
    }

    [Fact]
    public void RetainsInnerQuotesAsString()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["RETAIN_INNER_QUOTES_AS_STRING"].ShouldBe("{\"foo\": \"bar\"}");
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
    public void ParsesMultiLineValuesInDoubleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["MULTI_DOUBLE_QUOTED"].ShouldBe("THIS\nIS\nA\nMULTILINE\nSTRING");
    }

    [Fact]
    public void ParsesMultiLineValuesInSingleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["MULTI_SINGLE_QUOTED"].ShouldBe("THIS\nIS\nA\nMULTILINE\nSTRING");
    }

    [Fact]
    public void ParsesMultiLineValuesInBackticks()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["MULTI_BACKTICKED"].ShouldBe("THIS\nIS\nA\n\"MULTILINE'S\"\nSTRING");
    }

    [Fact]
    public void ParsesMultiLinePemKeyInDoubleQuotes()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["MULTI_PEM_DOUBLE_QUOTED"].ShouldBe("-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAnNl1tL3QjKp3DZWM0T3u\nLgGJQwu9WqyzHKZ6WIA5T+7zPjO1L8l3S8k8YzBrfH4mqWOD1GBI8Yjq2L1ac3Y/\nbTdfHN8CmQr2iDJC0C6zY8YV93oZB3x0zC/LPbRYpF8f6OqX1lZj5vo2zJZy4fI/\nkKcI5jHYc8VJq+KCuRZrvn+3V+KuL9tF9v8ZgjF2PZbU+LsCy5Yqg1M8f5Jp5f6V\nu4QuUoobAgMBAAE=\n-----END PUBLIC KEY-----");
    }

    [Fact]
    public void ParsesSplitKeyValueLines()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SPLIT_KEY_VALUE_LINES"].ShouldBe("split line");
    }

    [Fact]
    public void ParsesSplitKeyValueLinesWithSpaces()
    {
        IDictionary<string, string> parsed = Main.Parse(_src);
        parsed["SPLIT_KEY_VALUE_SPACED_LINES"].ShouldBe("split line with spaces");
    }
}