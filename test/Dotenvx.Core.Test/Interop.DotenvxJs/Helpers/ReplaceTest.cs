using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/replace.test.js</remarks>
public class ReplaceTest
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

        string newSrc = Replace(Src, "DOESNOTEXIST", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["DOESNOTEXIST"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "appends when ending newline"</remarks>
    [Fact]
    public void TestAppendsWhenEndingNewline()
    {
        string srcWithNewline = Src + "\n\n";

        string expected = srcWithNewline + "DOESNOTEXIST=\"ttt\"\n";

        string newSrc = Replace(srcWithNewline, "DOESNOTEXIST", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["DOESNOTEXIST"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#HELLO"</remarks>
    [Fact]
    public void TestReplaceHello()
    {
        string expected = Src.Replace("HELLO=a", "HELLO=ttt");

        string newSrc = Replace(Src, "HELLO", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["HELLO"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SPACER"</remarks>
    [Fact]
    public void TestReplaceSpacer()
    {
        string expected = Src.Replace("SPACER= b", "SPACER=ttt");

        string newSrc = Replace(Src, "SPACER", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["SPACER"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SPACEL"</remarks>
    [Fact]
    public void TestReplaceSpacel()
    {
        string expected = Src.Replace("SPACEL =c", "SPACEL=ttt");

        string newSrc = Replace(Src, "SPACEL", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["SPACEL"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SPACEB"</remarks>
    [Fact]
    public void TestReplaceSpaceb()
    {
        string expected = Src.Replace("SPACEB = d", "SPACEB=ttt");

        string newSrc = Replace(Src, "SPACEB", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["SPACEB"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#SINGLE"</remarks>
    [Fact]
    public void TestReplaceSingle()
    {
        string expected = Src.Replace("SINGLE='e'", "SINGLE='ttt'");

        string newSrc = Replace(Src, "SINGLE", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["SINGLE"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#DOUBLE"</remarks>
    [Fact]
    public void TestReplaceDouble()
    {
        string expected = Src.Replace("DOUBLE=\"f\"", "DOUBLE=\"ttt\"");

        string newSrc = Replace(Src, "DOUBLE", "ttt");

        newSrc.ShouldBe(expected);
        DotenvParse(newSrc, true, true)["DOUBLE"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "#replace"</remarks>
    [Fact]
    public void TestReplaceSimple()
    {
        string src = "HELLO=World";

        string newSrc = Replace(src, "HELLO", "Universe");

        newSrc.ShouldBe("HELLO=Universe");
    }

    /// <remarks>Refer to "#replace with single quotes"</remarks>
    [Fact]
    public void TestReplaceWithSingleQuotes()
    {
        string src = "HELLO='World'";

        string newSrc = Replace(src, "HELLO", "Universe");

        newSrc.ShouldBe("HELLO='Universe'");
    }

    /// <remarks>Refer to "#replace with double quotes"</remarks>
    [Fact]
    public void TestReplaceWithDoubleQuotes()
    {
        string src = "HELLO=\"World\"";

        string newSrc = Replace(src, "HELLO", "Universe");

        newSrc.ShouldBe("HELLO=\"Universe\"");
    }

    /// <remarks>Refer to "#replace appends when key does not yet exist"</remarks>
    [Fact]
    public void TestReplaceAppendsWhenKeyDoesNotExist()
    {
        string src = "HELLO=World";

        string newSrc = Replace(src, "API_KEY", "1234");

        newSrc.ShouldBe("HELLO=World\nAPI_KEY=\"1234\"");
    }

    /// <remarks>Refer to "#replace appends smartly if ending newline already"</remarks>
    [Fact]
    public void TestReplaceAppendsSmartlyIfEndingNewline()
    {
        string src = "HELLO=World\n";

        string newSrc = Replace(src, "API_KEY", "1234");

        newSrc.ShouldBe("HELLO=World\nAPI_KEY=\"1234\"\n");
    }
}