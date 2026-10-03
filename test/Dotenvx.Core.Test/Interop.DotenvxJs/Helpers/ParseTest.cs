using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/parse.test.js</remarks>
public class ParseTest
{
    private const string Src = @"#!/usr/bin/env bash
HELLO=world
export EXPORT=export

# previous line intentional left blank
AFTER_LINE=after_line
EMPTY=
EMPTY_SINGLE_QUOTES=''
EMPTY_DOUBLE_QUOTES=""""
EMPTY_BACKTICKS=``
SINGLE_QUOTES='single_quotes'
SINGLE_QUOTES_SPACED='    single quotes    '
DOUBLE_QUOTES=""double_quotes""
DOUBLE_QUOTES_SPACED=""    double quotes    ""
DOUBLE_QUOTES_INSIDE_SINGLE='double ""quotes"" work inside single quotes'
# DOUBLE_QUOTES_WITH_NO_SPACE_BRACKET=""{ port: $MONGOLAB_PORT}""
SINGLE_QUOTES_INSIDE_DOUBLE=""single 'quotes' work inside double quotes""
BACKTICKS_INSIDE_SINGLE='`backticks` work inside single quotes'
BACKTICKS_INSIDE_DOUBLE=""`backticks` work inside double quotes""
BACKTICKS=`backticks`
BACKTICKS_SPACED=`    backticks    `
DOUBLE_QUOTES_INSIDE_BACKTICKS=`double ""quotes"" work inside backticks`
SINGLE_QUOTES_INSIDE_BACKTICKS=`single 'quotes' work inside backticks`
DOUBLE_AND_SINGLE_QUOTES_INSIDE_BACKTICKS=`double ""quotes"" and single 'quotes' work inside backticks`
EXPAND_NEWLINES=""expand\nnew\nlines""
DONT_EXPAND_UNQUOTED=dontexpand\nnewlines
DONT_EXPAND_SQUOTED='dontexpand\nnewlines'
# COMMENTS=work
INLINE_COMMENTS=inline comments # work #very #well
INLINE_COMMENTS_SINGLE_QUOTES='inline comments outside of #singlequotes' # work
INLINE_COMMENTS_DOUBLE_QUOTES=""inline comments outside of #doublequotes"" # work
INLINE_COMMENTS_BACKTICKS=`inline comments outside of #backticks` # work
INLINE_COMMENTS_SPACE=inline comments start with a#number sign. no space required.
EQUAL_SIGNS=equals==
RETAIN_INNER_QUOTES={""foo"": ""bar""}
RETAIN_INNER_QUOTES_AS_STRING='{""foo"": ""bar""}'
RETAIN_INNER_QUOTES_AS_BACKTICKS=`{""foo"": ""bar's""}`
TRIM_SPACE_FROM_UNQUOTED=    some spaced out string
USERNAME=therealnerdybeast@example.tld
    SPACED_KEY = parsed
ENCRYPTED=""encrypted:BG8M6U+GKJGwpGA42ml2erb9+T2NBX6Z2JkBLynDy21poz0UfF5aPxCgRbIyhnQFdWKd0C9GZ7lM5PeL86xghoMcWvvPpkyQ0yaD2pZ64RzoxFGB1lTZYlEgQOxTDJnWxODHfuQcFY10uA==""
ECHO=""$(echo echo)""
ECHO_SQUOTED='$(echo echo)'
ECHO_UNQUOTED=$(echo echo)
BASIC=basic
BASIC_EXPAND=$BASIC
MACHINE=file
MACHINE_EXPAND=$MACHINE
ESCAPED_EXPAND=\$ESCAPED
EXPAND_DEFAULT=${MACHINE:-default}
EXPAND_DEFAULT_NESTED=${MACHINE:-${UNDEFINED:-default}}
EXPAND_DEFAULT_NESTED2=${MACHINE-${UNDEFINED-default}}
EXPAND_DEFAULT_NESTED_TWICE=${UNDEFINED:-${MACHINE}${UNDEFINED:-default}}
EXPAND_DEFAULT_NESTED_TWICE2=${UNDEFINED-${MACHINE}${UNDEFINED-default}}
EXPAND_DEFAULT_SPECIAL_CHARACTERS=${MACHINE:-/default/path:with/colon}
EXPAND_DEFAULT_SPECIAL_CHARACTERS2=${MACHINE-/default/path:with/colon}
UNDEFINED_EXPAND=$UNDEFINED
UNDEFINED_EXPAND_NESTED=${UNDEFINED:-${MACHINE:-default}}
UNDEFINED_EXPAND_DEFAULT=${UNDEFINED:-default}
UNDEFINED_EXPAND_DEFAULT2=${UNDEFINED-default}
UNDEFINED_EXPAND_DEFAULT_NESTED=${UNDEFINED:-${UNDEFINED:-default}}
UNDEFINED_EXPAND_DEFAULT_NESTED2=${UNDEFINED-${UNDEFINED-default}}
UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE=${UNDEFINED:-${UNDEFINED:-${UNDEFINED:-default}}}
UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE2=${UNDEFINED-${UNDEFINED-${UNDEFINED-default}}}
UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS=${UNDEFINED:-/default/path:with/colon}
UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS2=${UNDEFINED-/default/path:with/colon}
UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS_NESTED=${UNDEFINED:-${UNDEFINED_2:-/default/path:with/colon}}
UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS_NESTED2=${UNDEFINED-${UNDEFINED_2-/default/path:with/colon}}
# https://github.com/dotenvx/dotenvx/issues/422#issuecomment-2438293073
SINGLE_QUOTE='$BASIC'

# https://github.com/dotenvx/dotenvx/issues/433
DEEP8=${QUXX:-prefix5-${QUX:-prefix4-${BAZ:-prefix3-${BAR:-prefix2-${FOO:-prefix1-${BASIC:-test}-suffix1}-suffix2}-suffix3}-suffix4}-suffix5}
DEEP_SELF=${DEEP_SELF:-${BASIC:-test}-bar}
DEEP_SELF_PRIOR=foo
DEEP_SELF_PRIOR=prefix2-${DEEP_SELF_PRIOR:-prefix1-${BASIC:-test}-suffix2}-suffix2

NO_CURLY_BRACES_UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS=$UNDEFINED:-/default/path:with/colon
NO_CURLY_BRACES_UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS2=$UNDEFINED-/default/path:with/colon

# progressive update
PROGRESSIVE=first
PROGRESSIVE=${PROGRESSIVE}-second

DASH=hi-dash

USE_IF_SET=true
ALTERNATE=${USE_IF_SET:+alternate}

# https://github.com/motdotla/dotenv-expand/issues/98
EXPAND_SELF=$EXPAND_SELF
";

    /// <remarks>Refer to "respects backticks inside double quotes"</remarks>
    [Fact]
    public void TestRespectsBackticksInsideDoubleQuotes()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["BACKTICKS_INSIDE_DOUBLE"].ShouldBe("`backticks` work inside double quotes");
    }

    /// <remarks>Refer to "parses backticks"</remarks>
    [Fact]
    public void TestParsesBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["BACKTICKS"].ShouldBe("backticks");
    }

    /// <remarks>Refer to "parses backticks spaced"</remarks>
    [Fact]
    public void TestParsesBackticksSpaced()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["BACKTICKS_SPACED"].ShouldBe("    backticks    ");
    }

    /// <remarks>Refer to "respects double quotes inside backticks"</remarks>
    [Fact]
    public void TestRespectsDoubleQuotesInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["DOUBLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("double \"quotes\" work inside backticks");
    }

    /// <remarks>Refer to "respects single quotes inside backticks"</remarks>
    [Fact]
    public void TestRespectsSingleQuotesInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["SINGLE_QUOTES_INSIDE_BACKTICKS"].ShouldBe("single 'quotes' work inside backticks");
    }

    /// <remarks>Refer to "respects double and single quotes inside backticks"</remarks>
    [Fact]
    public void TestRespectsDoubleAndSingleQuotesInsideBackticks()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["DOUBLE_AND_SINGLE_QUOTES_INSIDE_BACKTICKS"]
            .ShouldBe("double \"quotes\" and single 'quotes' work inside backticks");
    }

    /// <remarks>Not from a JS test - expectations read off the live @dotenvx/primitives parse() on 2026-10-02: ':' operators treat empty as unset, bare operators only treat absent as unset.</remarks>
    [Fact]
    public void TestExpandOperatorsTreatEmptyValuesLikePrimitives()
    {
        const string src = "E=\nS=\nA=${E:-d}\nB=${E-d}\nC=${E:+x}\nD=${E+x}\nF=${S}y\nG=${Z-d}\nH=${Z+x}\nX=${Z:-a-b}\n";

        IReadOnlyDictionary<string, string> parsed = Main.Parse(src);

        parsed["A"].ShouldBe("d");
        parsed["B"].ShouldBe("");
        parsed["C"].ShouldBe("");
        parsed["D"].ShouldBe("x");
        parsed["F"].ShouldBe("y");
        parsed["G"].ShouldBe("d");
        parsed["H"].ShouldBe("");
        parsed["X"].ShouldBe("a-b");
    }

    /// <remarks>Refer to "expands newlines but only if double quoted"</remarks>
    [Fact]
    public void TestExpandsNewlinesButOnlyIfDoubleQuoted()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["EXPAND_NEWLINES"].ShouldBe("expand\nnew\nlines");
    }

    /// <remarks>Refer to "does not expand newlines if unquoted"</remarks>
    [Fact]
    public void TestDoesNotExpandNewlinesIfUnquoted()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["DONT_EXPAND_UNQUOTED"].ShouldBe("dontexpand\\nnewlines");
    }

    /// <remarks>Refer to "does not expand newlines if single quoted"</remarks>
    [Fact]
    public void TestDoesNotExpandNewlinesIfSingleQuoted()
    {
        IReadOnlyDictionary<string, string> parsed = Main.Parse(Src);
        parsed["DONT_EXPAND_SQUOTED"].ShouldBe("dontexpand\\nnewlines");
    }

    /// <remarks>
    /// A failing $() command substitution is silently left unresolved with no reported error,
    /// unlike other parse-time failures (e.g. decryption). Verified against the real dotenvx CLI:
    /// @dotenvx/primitives' parse() swallows COMMAND_SUBSTITUTION_FAILED specifically.
    /// </remarks>
    [Fact]
    public void TestEvalFailureIsSilentlyLeftUnresolvedWithNoError()
    {
        ParseResult result = Parse.Factory("BAD=\"$(exit 1)\"", null, null, false, null).Execute();

        result.Errors.ShouldBeEmpty();
        result.Parsed["BAD"].ShouldBe("$(exit 1)");
    }
}