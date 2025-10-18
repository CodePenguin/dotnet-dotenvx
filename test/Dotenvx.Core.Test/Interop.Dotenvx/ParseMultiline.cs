using Dotenvx.Core.Interop.DotenvxJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx;

public class ParseMultiline
{
    /// <summary>
    /// Equivalent tests from https://github.com/dotenvx/dotenvx/blob/main/tests/lib/parse-multiline.test.js
    /// </summary>
    [Fact]
    public void TestDotenvxMultilineParseTests()
    {
        var src = File.ReadAllText("tests/.env.multiline");
        var parsed = Main.Parse(src);
        parsed["BASIC"].ShouldBe("basic", "sets basic environment variable");
        parsed["AFTER_LINE"].ShouldBe("after_line", "reads after a skipped line");
        parsed["EMPTY"].ShouldBe("", "defaults empty values to empty string");
        parsed["SINGLE_QUOTES"].ShouldBe("single_quotes", "escapes single quoted values");
        parsed["SINGLE_QUOTES_SPACED"].ShouldBe("    single quotes    ", "respects surrounding spaces in single quotes");
        parsed["DOUBLE_QUOTES"].ShouldBe("double_quotes", "escapes double quoted values");
        parsed["DOUBLE_QUOTES_SPACED"].ShouldBe("    double quotes    ", "respects surrounding spaces in double quotes");
        parsed["EXPAND_NEWLINES"].ShouldBe("expand\nnew\nlines", "expands newlines but only if double quoted");
        parsed["DONT_EXPAND_UNQUOTED"].ShouldBe("dontexpand\\nnewlines", "does not expand newlines if unquoted");
        parsed["DONT_EXPAND_SQUOTED"].ShouldBe("dontexpand\\nnewlines", "does not expand newlines if single quoted");
        parsed["EQUAL_SIGNS"].ShouldBe("equals==", "respects equals signs in values");
        parsed["RETAIN_INNER_QUOTES"].ShouldBe("{\"foo\": \"bar\"}", "retains inner quotes");
        parsed["RETAIN_INNER_QUOTES_AS_STRING"].ShouldBe("{\"foo\": \"bar\"}", "retains inner quotes as string");
        parsed["TRIM_SPACE_FROM_UNQUOTED"].ShouldBe("some spaced out string", "trims spaces from unquoted values");
        parsed["USERNAME"].ShouldBe("therealnerdybeast@example.tld", "parses email addresses completely");
        parsed["SPACED_KEY"].ShouldBe("parsed", "parses keys and values surrounded by spaces");
        parsed["MULTI_DOUBLE_QUOTED"].ShouldBe("THIS\nIS\nA\nMULTILINE\nSTRING", "parses multi-line values in double quotes");
        parsed["MULTI_SINGLE_QUOTED"].ShouldBe("THIS\nIS\nA\nMULTILINE\nSTRING", "parses multi-line values in single quotes");
        parsed["MULTI_BACKTICKED"].ShouldBe("THIS\nIS\nA\n\"MULTILINE'S\"\nSTRING", "parses multi-line values in backticks");
        parsed["MULTI_PEM_DOUBLE_QUOTED"].ShouldBe("-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAnNl1tL3QjKp3DZWM0T3u\nLgGJQwu9WqyzHKZ6WIA5T+7zPjO1L8l3S8k8YzBrfH4mqWOD1GBI8Yjq2L1ac3Y/\nbTdfHN8CmQr2iDJC0C6zY8YV93oZB3x0zC/LPbRYpF8f6OqX1lZj5vo2zJZy4fI/\nkKcI5jHYc8VJq+KCuRZrvn+3V+KuL9tF9v8ZgjF2PZbU+LsCy5Yqg1M8f5Jp5f6V\nu4QuUoobAgMBAAE=\n-----END PUBLIC KEY-----", "parses multi-line PEM key in double quotes");
        
        // Additional Scenarios
        parsed["SPLIT_KEY_VALUE_LINES"].ShouldBe("split line", "parses split key value lines");
        parsed["SPLIT_KEY_VALUE_SPACED_LINES"].ShouldBe("split line with spaces", "parses split key value lines with spaces");
    }
}