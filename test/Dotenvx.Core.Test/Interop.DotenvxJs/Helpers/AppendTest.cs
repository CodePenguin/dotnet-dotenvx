using Dotenvx.Interop.DotenvxJs;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/append.test.js</remarks>
public class AppendTest
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
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

    /// <remarks>Refer to "appends new key"</remarks>
    [Fact]
    public void TestAppendsNewKey()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "DOESNOTEXIST", "ttt");
        newSrc.ShouldBe(expected + "\nDOESNOTEXIST=\"ttt\"");
        Main.Parse(newSrc)["DOESNOTEXIST"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "appends when ending newline"</remarks>
    [Fact]
    public void TestAppendsWhenEndingNewline()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'


";

        string newSrc = Append(Src, "DOESNOTEXIST", "ttt");
        newSrc.ShouldBe(expected.Trim() + "\nDOESNOTEXIST=\"ttt\"");
        Main.Parse(newSrc)["DOESNOTEXIST"].ShouldBe("ttt");
    }

    /// <remarks>Refer to "HELLO"</remarks>
    [Fact]
    public void TestHello()
    {
        const string expected = @"HELLO=a,ttt
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "HELLO", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["HELLO"].ShouldBe("a,ttt");
    }

    /// <remarks>Refer to "#SPACER"</remarks>
    [Fact]
    public void TestSpacer()
    {
        const string expected = @"HELLO=a
SPACER=b,ttt
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "SPACER", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["SPACER"].ShouldBe("b,ttt");
    }

    /// <remarks>Refer to "#SPACEL"</remarks>
    [Fact]
    public void TestSpacel()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL=c,ttt
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "SPACEL", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["SPACEL"].ShouldBe("c,ttt");
    }

    /// <remarks>Refer to "#SPACEB"</remarks>
    [Fact]
    public void TestSpaceb()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB=d,ttt
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "SPACEB", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["SPACEB"].ShouldBe("d,ttt");
    }

    /// <remarks>Refer to "#SINGLE"</remarks>
    [Fact]
    public void TestSingle()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e,ttt'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "SINGLE", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["SINGLE"].ShouldBe("e,ttt");
    }

    /// <remarks>Refer to "#DOUBLE"</remarks>
    [Fact]
    public void TestDouble()
    {
        string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f,ttt""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "DOUBLE", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["DOUBLE"].ShouldBe("f,ttt");
    }

    /// <remarks>Refer to "#JSON"</remarks>
    [Fact]
    public void TestJson()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1},{""other"": 2}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "JSON", "{\"other\": 2}");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["JSON"].ShouldBe("{\"hi\": 1},{\"other\": 2}");
    }

    /// <remarks>Refer to "#JSON2"</remarks>
    [Fact]
    public void TestJson2()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1},{""hi"": 2}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "JSON2", "{\"hi\": 2}");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["JSON2"].ShouldBe("{\"hi\": 1},{\"hi\": 2}");
    }

    /// <remarks>Refer to "#QUOTE"</remarks>
    [Fact]
    public void TestQuote()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""',""""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "QUOTE", "\"");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["QUOTE"].ShouldBe("',\"");
    }
    
    /// <remarks>Refer to "#QUOTE2"</remarks>
    [Fact]
    public void TestQuote2()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='"",''
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "QUOTE2", "'");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["QUOTE2"].ShouldBe("\",'");
    }

    /// <remarks>Refer to "#EXPORT"</remarks>
    [Fact]
    public void TestExport()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
export EXPORT=k,ttt
EXPORT2='export EXPORT=k'
export EXPORT=k,ttt
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "EXPORT", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["EXPORT"].ShouldBe("k,ttt");
    }

    /// <remarks>Refer to "#PAD"</remarks>
    [Fact]
    public void TestPad()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
export EXPORT=k
EXPORT2='export EXPORT=k'
export EXPORT=k
  PAD=l,ttt
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "PAD", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["PAD"].ShouldBe("l,ttt");
    }

    /// <remarks>Refer to "#BAD"</remarks>
    [Fact]
    public void TestBad()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
export EXPORT=k
EXPORT2='export EXPORT=k'
export EXPORT=k
  PAD=l
BAD=f'bar,f""bar
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "BAD", "f\"bar");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["BAD"].ShouldBe("f'bar,f\"bar");
    }

    /// <remarks>Refer to "#BAD2"</remarks>
    [Fact]
    public void TestBad2()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
export EXPORT=k
EXPORT2='export EXPORT=k'
export EXPORT=k
  PAD=l
BAD=f'bar
BAD2=f""bar,f'bar
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "BAD2", "f'bar");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["BAD2"].ShouldBe("f\"bar,f'bar");
    }

    /// <remarks>Refer to "#MULTI"</remarks>
    [Fact]
    public void TestMulti()
    {
        string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
export EXPORT=k
EXPORT2='export EXPORT=k'
export EXPORT=k
  PAD=l
BAD=f'bar
BAD2=f""bar
MULTI='-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----,ttt'
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "MULTI", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["MULTI"].ShouldBe(@"-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----,ttt");
    }

    /// <remarks>Refer to "#MULTI2"</remarks>
    [Fact]
    public void TestMulti2()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
-----END RSA PRIVATE KEY-----,hi
my
friend""
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
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "MULTI2", @"hi
my
friend");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["MULTI2"].ShouldBe(@"-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----,hi
my
friend");
    }

    /// <remarks>Refer to "#MULTI3"</remarks>
    [Fact]
    public void TestMulti3()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
-----END RSA PRIVATE KEY-----,hi
my
friend`
EVAL=$(echo world)
EMPTY=
NEWLINES=""expand\nnew\nlines""
COMMENT=g # comment
HASHTAG=""h #tag""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "MULTI3", @"hi
my
friend");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["MULTI3"].ShouldBe(@"-----BEGIN RSA PRIVATE KEY-----
ABCD
-----END RSA PRIVATE KEY-----,hi
my
friend");
    }

    /// <remarks>Refer to "#EVAL"</remarks>
    [Fact]
    public void TestEval()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
EVAL=$(echo world),ttt
EMPTY=
NEWLINES=""expand\nnew\nlines""
COMMENT=g # comment
HASHTAG=""h #tag""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "EVAL", "ttt");
        newSrc.ShouldBe(expected);
        // Note: Eval is not currently supported
        Main.Parse(newSrc)["EVAL"].ShouldBe("$(echo world),ttt");
        // Main.Parse(newSrc)["EVAL"].ShouldBe("world,ttt"); // Uncomment and replace when supported
    }

    /// <remarks>Refer to "#EMPTY"</remarks>
    [Fact]
    public void TestEmpty()
    {
        string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
EMPTY=,ttt
NEWLINES=""expand\nnew\nlines""
COMMENT=g # comment
HASHTAG=""h #tag""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "EMPTY", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["EMPTY"].ShouldBe(",ttt");
    }

    /// <remarks>Refer to "#NEWLINES"</remarks>
    [Fact]
    public void TestNewlines()
    {
        string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
NEWLINES=""expand\nnew\nlines,ttt""
COMMENT=g # comment
HASHTAG=""h #tag""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "NEWLINES", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["NEWLINES"].ShouldBe("expand\nnew\nlines,ttt");
    }

    /// <remarks>Refer to "#COMMENT"</remarks>
    [Fact]
    public void TestComment()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
COMMENT=g,ttt # comment
HASHTAG=""h #tag""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "COMMENT", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["COMMENT"].ShouldBe("g,ttt");
    }

    /// <remarks>Refer to "#HASHTAG"</remarks>
    [Fact]
    public void TestHashtag()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
HASHTAG=""h #tag,ttt""
D.O.T.S=i
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "HASHTAG", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["HASHTAG"].ShouldBe("h #tag,ttt");
    }

    /// <remarks>Refer to "#D.O.T.S"</remarks>
    [Fact]
    public void TestDots()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
D.O.T.S=i,ttt
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "D.O.T.S", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["D.O.T.S"].ShouldBe("i,ttt");
    }

    /// <remarks>Refer to "#DONT_CHOKE1"</remarks>
    [Fact]
    public void TestDontChoke1()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
DONT_CHOKE1='.kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!,ttt'
ESCAPED=`ESCAPED
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "DONT_CHOKE1", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["DONT_CHOKE1"].ShouldBe(".kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!,ttt");
    }

    /// <remarks>Refer to "#ESCAPED"</remarks>
    [Fact]
    public void TestEscaped()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED=`ESCAPED,ttt
ESCAPED2='test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "ESCAPED", "ttt");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["ESCAPED"].ShouldBe("`ESCAPED,ttt");
    }

    /// <remarks>Refer to "#ESCAPED2 replacing itself"</remarks>
    [Fact]
    public void TestEscaped2ReplacingItself()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test,test\test\test\test'
GROUP='$1$2'";

        string newSrc = Append(Src, "ESCAPED2", @"test\test\test\test");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["ESCAPED2"].ShouldBe(@"test\test,test\test\test\test");
    }

    /// <remarks>Refer to "#GROUP"</remarks>
    [Fact]
    public void TestGroup()
    {
        const string expected = @"HELLO=a
SPACER= b
SPACEL =c
SPACEB = d
SINGLE='e'
DOUBLE=""f""
BACKTICK=`g`
JSON={""hi"": 1}
JSON2=""{""hi"": 1}""
QUOTE=""'""
QUOTE2='""'
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
ESCAPED2='test\test'
GROUP='$1$2,$bar$baz$paz$1234'";

        string newSrc = Append(Src, "GROUP", "$bar$baz$paz$1234");
        newSrc.ShouldBe(expected);
        Main.Parse(newSrc)["GROUP"].ShouldBe("$1$2,$bar$baz$paz$1234");
    }

    /// <remarks>Refer to "#append"</remarks>
    [Fact]
    public void TestAppend()
    {
        const string src = "HELLO=World";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=World,Universe");
    }

    /// <remarks>Refer to "#append with single quotes"</remarks>
    [Fact]
    public void TestAppendWithSingleQuotes()
    {
        const string src = "HELLO='World'";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO='World,Universe'");
    }

    /// <remarks>Refer to "#append with double quotes"</remarks>
    [Fact]
    public void TestAppendWithDoubleQuotes()
    {
        const string src = "HELLO=\"World\"";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=\"World,Universe\"");
    }

    /// <remarks>Refer to "#append appends when key does not yet exist"</remarks>
    [Fact]
    public void TestAppendAppendsWhenKeyDoesNotYetExist()
    {
        const string src = "HELLO=World";
        string newSrc = Append(src, "API_KEY", "1234");
        newSrc.ShouldBe("HELLO=World\nAPI_KEY=\"1234\"");
    }

    /// <remarks>Refer to "#append appends smartly if ending newline already"</remarks>
    [Fact]
    public void TestAppendAppendsSmartlyIfEndingNewlineAlready()
    {
        const string src = "HELLO=World\n";
        string newSrc = Append(src, "API_KEY", "1234");
        newSrc.ShouldBe("HELLO=World\nAPI_KEY=\"1234\"\n");
    }

    /// <remarks>Refer to "#append with double quoted multiline"</remarks>
    [Fact]
    public void TestAppendWithDoubleQuotedMultiline()
    {
        const string src = @"HELLO=""-----BEGIN RSA PRIVATE KEY-----
ABCD
EFGH
JKLM
-----END RSA PRIVATE KEY-----""";

        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe(@"HELLO=""-----BEGIN RSA PRIVATE KEY-----
ABCD
EFGH
JKLM
-----END RSA PRIVATE KEY-----,Universe""");
    }

    /// <remarks>Refer to "#append with single quoted multiline"</remarks>
    [Fact]
    public void TestAppendWithSingleQuotedMultiline()
    {
        const string src = @"HELLO='-----BEGIN RSA PRIVATE KEY-----
ABCD
EFGH
JKLM
-----END RSA PRIVATE KEY-----'";

        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe(@"HELLO='-----BEGIN RSA PRIVATE KEY-----
ABCD
EFGH
JKLM
-----END RSA PRIVATE KEY-----,Universe'");
    }

    /// <remarks>Refer to "#append with backtick quoted multiline"</remarks>
    [Fact]
    public void TestAppendWithBacktickQuotedMultiline()
    {
        const string src = @"HELLO=`-----BEGIN RSA PRIVATE KEY-----
THIS
IS
""MULTILINE's""
-----END RSA PRIVATE KEY-----`";

        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe(@"HELLO=`-----BEGIN RSA PRIVATE KEY-----
THIS
IS
""MULTILINE's""
-----END RSA PRIVATE KEY-----,Universe`");
    }

    /// <remarks>Refer to "#append evals"</remarks>
    [Fact]
    public void TestAppendEvals()
    {
        const string src = "HELLO=\"$(echo world)\"";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=\"$(echo world),Universe\"");
    }

    /// <remarks>Refer to "#append empty"</remarks>
    [Fact]
    public void TestAppendEmpty()
    {
        const string src = "HELLO=";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=,Universe");
    }

    /// <remarks>Refer to "#append empty backticks"</remarks>
    [Fact]
    public void TestAppendEmptyBackticks()
    {
        const string src = "HELLO=``";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=`,Universe`");
    }

    /// <remarks>Refer to "#append spaced single quotes"</remarks>
    [Fact]
    public void TestAppendSpacedSingleQuotes()
    {
        const string src = "HELLO='    single quote   '";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO='    single quote   ,Universe'");
    }

    /// <remarks>Refer to "#append spaced double quotes"</remarks>
    [Fact]
    public void TestAppendSpacedDoubleQuotes()
    {
        const string src = "HELLO=\"    single quote   \"";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=\"    single quote   ,Universe\"");
    }

    /// <remarks>Refer to "#append double quotes inside single quotes"</remarks>
    [Fact]
    public void TestAppendDoubleQuotesInsideSingleQuotes()
    {
        const string src = "HELLO='double \"quotes\" inside single quotes'";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO='double \"quotes\" inside single quotes,Universe'");
    }

    /// <remarks>Refer to "#append single quotes inside double quotes"</remarks>
    [Fact]
    public void TestAppendSingleQuotesInsideDoubleQuotes()
    {
        const string src = "HELLO=\"single 'quotes' inside single quotes\"";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=\"single 'quotes' inside single quotes,Universe\"");
    }

    /// <remarks>Refer to "#append double quotes and single quotes inside backticks"</remarks>
    [Fact]
    public void TestAppendDoubleQuotesAndSingleQuotesInsideBackticks()
    {
        const string src = "HELLO=`double \"quotes\" and single 'quotes' inside backticks`";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=`double \"quotes\" and single 'quotes' inside backticks,Universe`");
    }

    /// <remarks>Refer to "#append newlines"</remarks>
    [Fact]
    public void TestAppendNewlines()
    {
        const string src = "HELLO=\"expand\nnew\nlines\"";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=\"expand\nnew\nlines,Universe\"");
    }

    /// <remarks>Refer to "#append inline comments"</remarks>
    [Fact]
    public void TestAppendInlineComments()
    {
        const string src = "HELLO=inline comments # work #very #well";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=inline comments,Universe # work #very #well");
    }

    /// <remarks>Refer to "#append hashtag in quotes"</remarks>
    [Fact]
    public void TestAppendHashtagInQuotes()
    {
        const string src = "HELLO=\"hash #tag quoted\"";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=\"hash #tag quoted,Universe\"");
    }

    /// <remarks>Refer to "#append equal signs"</remarks>
    [Fact]
    public void TestAppendEqualSigns()
    {
        const string src = "HELLO=equals==";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=equals==,Universe");
    }

    /// <remarks>Refer to "#append inner quotes"</remarks>
    [Fact]
    public void TestAppendInnerQuotes()
    {
        const string src = "HELLO={\"foo\": \"bar\"}";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO={\"foo\": \"bar\"},Universe");
    }

    /// <remarks>Refer to "#append inner quotes as string"</remarks>
    [Fact]
    public void TestAppendInnerQuotesAsString()
    {
        const string src = "HELLO='{\"foo\": \"bar\"}'";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO='{\"foo\": \"bar\"},Universe'");
    }

    /// <remarks>Refer to "#append inner quotes as backticks"</remarks>
    [Fact]
    public void TestAppendInnerQuotesAsBackticks()
    {
        const string src = "HELLO=`{\"foo\": \"bar's\"}`";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("HELLO=`{\"foo\": \"bar's\"},Universe`");
    }

    /// <remarks>Refer to "#append spaced key"</remarks>
    [Fact]
    public void TestAppendSpacedKey()
    {
        const string src = "     HELLO=parsed";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("     HELLO=parsed,Universe");
    }

    /// <remarks>Refer to "#append somewhere in the middle"</remarks>
    [Fact]
    public void TestAppendSomewhereInTheMiddle()
    {
        const string src = @"HELLO=world
HELLO2=world
HELLO3=world
HELLO4=world";

        string newSrc = Append(src, "HELLO2", "universe");

        string expected = @"HELLO=world
HELLO2=world,universe
HELLO3=world
HELLO4=world";

        newSrc.ShouldBe(expected);
    }

    /// <remarks>Refer to "#append somewhere in the middle when double quoted"</remarks>
    [Fact]
    public void TestAppendSomewhereInTheMiddleWhenDoubleQuoted()
    {
        const string src = @"HELLO=""world""
HELLO2=""world""
HELLO3=""world""
HELLO4=""world""";

        string newSrc = Append(src, "HELLO3", "universe");

        string expected = @"HELLO=""world""
HELLO2=""world""
HELLO3=""world,universe""
HELLO4=""world""";

        newSrc.ShouldBe(expected);
    }

    /// <remarks>Refer to "#append somewhere in the middle when single quoted"</remarks>
    [Fact]
    public void TestAppendSomewhereInTheMiddleWhenSingleQuoted()
    {
        const string src = @"HELLO='world'
HELLO2='world'
HELLO3='world'
HELLO4='world'";

        string newSrc = Append(src, "HELLO3", "universe");

        string expected = @"HELLO='world'
HELLO2='world'
HELLO3='world,universe'
HELLO4='world'";

        newSrc.ShouldBe(expected);
    }

    /// <remarks>Refer to "#append somewhere in the middle when backtick quoted"</remarks>
    [Fact]
    public void TestAppendSomewhereInTheMiddleWhenBacktickQuoted()
    {
        const string src = @"HELLO=`world`
HELLO2=`world`
HELLO3=`world`
HELLO4=`world``";

        string newSrc = Append(src, "HELLO3", "universe");

        string expected = @"HELLO=`world`
HELLO2=`world`
HELLO3=`world,universe`
HELLO4=`world``";

        newSrc.ShouldBe(expected);
    }

    /// <remarks>Refer to "#append with export"</remarks>
    [Fact]
    public void TestAppendWithExport()
    {
        const string src = "export HELLO=World";
        string newSrc = Append(src, "HELLO", "Universe");
        newSrc.ShouldBe("export HELLO=World,Universe");
    }

    /// <remarks>Refer to "#append multiline CRLF (\\r\\n)"</remarks>
    [Fact]
    public void TestAppendMultilineCrlf()
    {
        const string src = "MULTI_CRLF='-----BEGIN RSA PRIVATE KEY-----\r\nABCD\r\n-----END RSA PRIVATE KEY-----'";
        string newSrc = Append(src, "MULTI_CRLF", "Universe");
        newSrc.ShouldBe("MULTI_CRLF='-----BEGIN RSA PRIVATE KEY-----\r\nABCD\r\n-----END RSA PRIVATE KEY-----,Universe'");
    }
}