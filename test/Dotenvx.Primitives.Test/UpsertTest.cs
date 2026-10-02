using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/replace.test.js</remarks>
public class UpsertTest
{
    private const string Src = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2=""""""
export EXPORT=k
EXPORT2='export EXPORT=k'
export EXPORT=k
   PAD=l
BAD=f'bar
BAD2=f""bar
MULTI='-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----'
MULTI2=""-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----""
MULTI3=`-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----`
EVAL=$(echo world)
EMPTY=
NEWLINES=""expand\nnew\nlines""
COMMENT=g # comment
HASHTAG=""h #tag""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\\test'
GROUP='$1$2'";

    /// <remarks>Refer to "appends new key"</remarks>
    [Fact]
    public void TestAppendsNewKey()
    {
        string expected = Src + "\nDOESNOTEXIST=\"ttt\"";

        string newSrc = Upsert(Src, "DOESNOTEXIST", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["DOESNOTEXIST"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "appends when ending newline"</remarks>
    [Fact]
    public void TestAppendsWhenEndingNewline()
    {
        string srcWithNewline = Src + "\n\n";

        string expected = srcWithNewline + "DOESNOTEXIST=\"ttt\"\n";

        string newSrc = Upsert(srcWithNewline, "DOESNOTEXIST", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["DOESNOTEXIST"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#HELLO"</remarks>
    [Fact]
    public void TestReplaceHello()
    {
        string expected = Src.Replace("HELLO=a", "HELLO=ttt");

        string newSrc = Upsert(Src, "HELLO", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["HELLO"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SPACER"</remarks>
    [Fact]
    public void TestReplaceSpacer()
    {
        string expected = Src.Replace("SPACER= b", "SPACER=ttt");

        string newSrc = Upsert(Src, "SPACER", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["SPACER"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SPACEL"</remarks>
    [Fact]
    public void TestReplaceSpacel()
    {
        string expected = Src.Replace("SPACEL =c", "SPACEL=ttt");

        string newSrc = Upsert(Src, "SPACEL", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["SPACEL"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SPACEB"</remarks>
    [Fact]
    public void TestReplaceSpaceb()
    {
        string expected = Src.Replace("SPACEB = d", "SPACEB=ttt");

        string newSrc = Upsert(Src, "SPACEB", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["SPACEB"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SINGLE"</remarks>
    [Fact]
    public void TestReplaceSingle()
    {
        string expected = Src.Replace("SINGLE='e'", "SINGLE='ttt'");

        string newSrc = Upsert(Src, "SINGLE", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["SINGLE"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#DOUBLE"</remarks>
    [Fact]
    public void TestReplaceDouble()
    {
        string expected = Src.Replace("DOUBLE=\"f\"", "DOUBLE=\"ttt\"");

        string newSrc = Upsert(Src, "DOUBLE", "ttt");

        newSrc.ShouldBe(expected);
        Parse(newSrc, true, true)["DOUBLE"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#replace"</remarks>
    [Fact]
    public void TestReplaceSimple()
    {
        string src = "HELLO=World";

        string newSrc = Upsert(src, "HELLO", "Universe");

        newSrc.ShouldBe("HELLO=Universe");
    }

    /// <remarks>Refer to "#replace with single quotes"</remarks>
    [Fact]
    public void TestReplaceWithSingleQuotes()
    {
        string src = "HELLO='World'";

        string newSrc = Upsert(src, "HELLO", "Universe");

        newSrc.ShouldBe("HELLO='Universe'");
    }

    /// <remarks>Refer to "#replace with double quotes"</remarks>
    [Fact]
    public void TestReplaceWithDoubleQuotes()
    {
        string src = "HELLO=\"World\"";

        string newSrc = Upsert(src, "HELLO", "Universe");

        newSrc.ShouldBe("HELLO=\"Universe\"");
    }

    /// <remarks>Refer to "#replace appends when key does not yet exist"</remarks>
    [Fact]
    public void TestReplaceAppendsWhenKeyDoesNotExist()
    {
        string src = "HELLO=World";

        string newSrc = Upsert(src, "API_KEY", "1234");

        newSrc.ShouldBe("HELLO=World\nAPI_KEY=\"1234\"");
    }

    /// <remarks>Refer to "#replace appends smartly if ending newline already"</remarks>
    [Fact]
    public void TestReplaceAppendsSmartlyIfEndingNewline()
    {
        string src = "HELLO=World\n";

        string newSrc = Upsert(src, "API_KEY", "1234");

        newSrc.ShouldBe("HELLO=World\nAPI_KEY=\"1234\"\n");
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-29 bug found investigating whether this
    /// file's upstream-doc-comment staleness (replace.js no longer exists; the current authority is
    /// @dotenvx/primitives' upsert()) hid a behavioral gap. Confirmed live against Node: a duplicate
    /// key with DIFFERENT values on each occurrence gets EVERY occurrence updated to the new value,
    /// not just the one whose text happened to match the last-value-wins parse result. This port's
    /// original single-value-text-matching regex only ever touched the LAST occurrence, silently
    /// leaving earlier duplicates stale.
    /// </remarks>
    [Fact]
    public void TestReplaceUpdatesEveryOccurrenceOfADuplicateKeyWithDifferentValues()
    {
        const string src = "FOO=old1\nBAR=x\nFOO=old2\n";

        string newSrc = Upsert(src, "FOO", "new");

        newSrc.ShouldBe("FOO=new\nBAR=x\nFOO=new\n");
    }

    /// <remarks>
    /// Not from a JS test - companion to the above, confirming each occurrence keeps its OWN
    /// original quote style rather than all converging on one quote (or none), matching Node's
    /// live-verified behavior exactly.
    /// </remarks>
    [Fact]
    public void TestReplaceEveryOccurrencePreservesItsOwnQuoteStyle()
    {
        const string src = "FOO=\"old1\"\nBAR=x\nFOO=old2\n";

        string newSrc = Upsert(src, "FOO", "new");

        newSrc.ShouldBe("FOO=\"new\"\nBAR=x\nFOO=new\n");
    }

    /// <remarks>
    /// Not from a JS test - the existing `Src` fixture already contains a duplicate `EXPORT` key
    /// (lines 21/23, both `export EXPORT=k`) with an `export`-prefixed line in between
    /// (`EXPORT2='export EXPORT=k'`) that must NOT be mistaken for a third occurrence. Confirms the
    /// multi-occurrence path correctly identifies real occurrences by parsed key, not by scanning
    /// for the raw substring, and preserves the `export ` prefix on both.
    /// </remarks>
    [Fact]
    public void TestReplaceUpdatesBothIdenticalExportPrefixedDuplicates()
    {
        string expected = Src
            .Replace("export EXPORT=k\nEXPORT2='export EXPORT=k'\nexport EXPORT=k",
                "export EXPORT=ttt\nEXPORT2='export EXPORT=k'\nexport EXPORT=ttt");

        string newSrc = Upsert(Src, "EXPORT", "ttt");

        newSrc.ShouldBe(expected);
    }
}