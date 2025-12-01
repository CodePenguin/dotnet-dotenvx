using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs;

/// <remarks>Reference source: dotenvx/tests/lib/config-expand.test.js</remarks>
public class ConfigExpandTest : EnvironmentTestBase
{
    private const string EnvExpandFileName = "tests/.env.expand";

    [Fact]
    public void TestExpands()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");

        result.Parsed["BASIC_EXPAND"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC_EXPAND").ShouldBe("basic");

        result.Parsed["MACHINE_EXPAND"].ShouldBe("file");
        Environment.GetEnvironmentVariable("MACHINE_EXPAND").ShouldBe("file");
    }

    [Fact]
    public void TestExpandsUsingMachineValueFirst()
    {
        Environment.SetEnvironmentVariable("MACHINE", "machine");
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["MACHINE"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("MACHINE").ShouldBe("machine");
        result.Parsed["MACHINE_EXPAND"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("MACHINE_EXPAND").ShouldBe("machine");
    }

    [Fact]
    public void TestExpandsToBringOwnProcessEnv()
    {
        Dictionary<string, string> myObject = new();
        ConfigOptions options = new() { Path = [EnvExpandFileName], ProcessEnv = myObject };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        myObject["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBeNull();

        result.Parsed["BASIC_EXPAND"].ShouldBe("basic");
        myObject["BASIC_EXPAND"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC_EXPAND").ShouldBeNull();

        result.Parsed["MACHINE_EXPAND"].ShouldBe("file");
        myObject["MACHINE_EXPAND"].ShouldBe("file");
        Environment.GetEnvironmentVariable("MACHINE_EXPAND").ShouldBeNull();
    }

    [Fact]
    public void TestExpandsEnvExpandCorrectly()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["ESCAPED_EXPAND"].ShouldBe("$ESCAPED");
        Environment.GetEnvironmentVariable("ESCAPED_EXPAND").ShouldBe("$ESCAPED");

        result.Parsed["ESCAPED_EXPAND"].ShouldBe("$ESCAPED");
        Environment.GetEnvironmentVariable("ESCAPED_EXPAND").ShouldBe("$ESCAPED");

        result.Parsed["EXPAND_DEFAULT"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT_NESTED"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT_NESTED2"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED2").ShouldBe("file");

        result.Parsed["UNDEFINED_EXPAND"].ShouldBe("");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND").ShouldBe(EmptyEnvironmentVariableValue);

        result.Parsed["UNDEFINED_EXPAND_NESTED"].ShouldBe("file");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_NESTED").ShouldBe("file");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT2"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT2").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED2"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED2").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE2"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE2").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS"].ShouldBe("/default/path:with/colon");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS")
            .ShouldBe("/default/path:with/colon");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS2"].ShouldBe("/default/path:with/colon");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS2")
            .ShouldBe("/default/path:with/colon");
    }

    [Fact]
    public void TestExpandsEnvExpandCorrectlyWhenMachineAlreadySet()
    {
        Environment.SetEnvironmentVariable("MACHINE", "machine");
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["ESCAPED_EXPAND"].ShouldBe("$ESCAPED");
        Environment.GetEnvironmentVariable("ESCAPED_EXPAND").ShouldBe("$ESCAPED");

        result.Parsed["EXPAND_DEFAULT"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT").ShouldBe("machine");

        result.Parsed["EXPAND_DEFAULT_NESTED"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED").ShouldBe("machine");

        result.Parsed["EXPAND_DEFAULT_NESTED2"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED2").ShouldBe("machine");

        result.Parsed["EXPAND_DEFAULT_NESTED_TWICE"].ShouldBe("machinedefault");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED_TWICE").ShouldBe("machinedefault");

        result.Parsed["EXPAND_DEFAULT_NESTED_TWICE2"].ShouldBe("machinedefault");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED_TWICE2").ShouldBe("machinedefault");

        result.Parsed["EXPAND_DEFAULT_SPECIAL_CHARACTERS"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_SPECIAL_CHARACTERS").ShouldBe("machine");

        result.Parsed["EXPAND_DEFAULT_SPECIAL_CHARACTERS2"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_SPECIAL_CHARACTERS2").ShouldBe("machine");

        result.Parsed["UNDEFINED_EXPAND"].ShouldBe("");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND").ShouldBe(EmptyEnvironmentVariableValue);

        result.Parsed["UNDEFINED_EXPAND_NESTED"].ShouldBe("machine");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_NESTED").ShouldBe("machine");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT2"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT2").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED2"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED2").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE2"].ShouldBe("default");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_NESTED_TWICE2").ShouldBe("default");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS"].ShouldBe("/default/path:with/colon");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS")
            .ShouldBe("/default/path:with/colon");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS2"].ShouldBe("/default/path:with/colon");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS2")
            .ShouldBe("/default/path:with/colon");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS_NESTED"].ShouldBe("/default/path:with/colon");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS_NESTED")
            .ShouldBe("/default/path:with/colon");

        result.Parsed["UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS_NESTED2"].ShouldBe("/default/path:with/colon");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_DEFAULT_SPECIAL_CHARACTERS_NESTED2")
            .ShouldBe("/default/path:with/colon");
    }

    [Fact]
    public void TestExpandsEnvExpandCorrectlyWhenMachineAlreadySetButOverloadIsTrue()
    {
        Environment.SetEnvironmentVariable("MACHINE", "machine");
        ConfigOptions options = new() { Path = [EnvExpandFileName], Overload = true };
        ConfigResult result = Main.Config(options);

        result.Parsed["MACHINE"].ShouldBe("file");
        Environment.GetEnvironmentVariable("MACHINE").ShouldBe("file");

        result.Parsed["MACHINE_EXPAND"].ShouldBe("file");
        Environment.GetEnvironmentVariable("MACHINE_EXPAND").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT_NESTED"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT_NESTED2"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED2").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT_NESTED_TWICE"].ShouldBe("filedefault");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED_TWICE").ShouldBe("filedefault");

        result.Parsed["EXPAND_DEFAULT_NESTED_TWICE2"].ShouldBe("filedefault");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_NESTED_TWICE2").ShouldBe("filedefault");

        result.Parsed["EXPAND_DEFAULT_SPECIAL_CHARACTERS"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_SPECIAL_CHARACTERS").ShouldBe("file");

        result.Parsed["EXPAND_DEFAULT_SPECIAL_CHARACTERS2"].ShouldBe("file");
        Environment.GetEnvironmentVariable("EXPAND_DEFAULT_SPECIAL_CHARACTERS2").ShouldBe("file");

        result.Parsed["UNDEFINED_EXPAND"].ShouldBe("");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND").ShouldBe(EmptyEnvironmentVariableValue);

        result.Parsed["UNDEFINED_EXPAND_NESTED"].ShouldBe("file");
        Environment.GetEnvironmentVariable("UNDEFINED_EXPAND_NESTED").ShouldBe("file");
    }

    [Fact]
    public void TestExpandsMongoRealWorldExample()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["MONGOLAB_URI"].ShouldBe("mongodb://username:password@abcd1234.mongolab.com:12345/heroku_db");
        result.Parsed["MONGOLAB_URI_RECURSIVELY"]
            .ShouldBe("mongodb://username:password@abcd1234.mongolab.com:12345/heroku_db");
        result.Parsed["NO_CURLY_BRACES_URI"]
            .ShouldBe("mongodb://username:password@abcd1234.mongolab.com:12345/heroku_db");
        result.Parsed["NO_CURLY_BRACES_URI_RECURSIVELY"]
            .ShouldBe("mongodb://username:password@abcd1234.mongolab.com:12345/heroku_db");
    }

    [Fact]
    public void TestExpandsWithPeriodsInKeyName()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["POSTGRESQL.MAIN.USER"].ShouldBe("postgres");
    }

    [Fact]
    public void TestDoesNotExpandDollar()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["DOLLAR"].ShouldBe("$");
    }

    [Fact]
    public void TestHandlesOneTwoVariables()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["ONETWO"].ShouldBe("onetwo");
        result.Parsed["ONETWO_SIMPLE"].ShouldBe("onetwo");
        result.Parsed["ONETWO_SIMPLE2"].ShouldBe("onetwo");
        result.Parsed["ONETWO_SUPER_SIMPLE"].ShouldBe("onetwo");
    }

    [Fact]
    public void TestHandlesTwoDollarSignsFollowedByDigit()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["TWO_DOLLAR_SIGNS_FOLLOWED_BY_DIGIT"].ShouldBe("abcd$$1234foo");
    }

    [Fact]
    public void TestHandlesTwoDollarSignsFollowedByLetter()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["TWO_DOLLAR_SIGNS_FOLLOWED_BY_LETTER"].ShouldBe("pa$@");
    }

    [Fact]
    public void TestHandlesTwoDollarSignsFollowedByLetterButSingleQuoted()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["TWO_DOLLAR_SIGNS_FOLLOWED_BY_LETTER_SINGLE_QUOTE"].ShouldBe("pa$$word@");
    }

    [Fact]
    public void TestDoesNotChoke()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["DONT_CHOKE1"].ShouldBe(".kZh`>4[,[DDU-*Jt+[;8-,@K=,9%;F9KsoXqOE)gpG^X!{)Q+/9Fc(QF}i[NEi!");
        result.Parsed["DONT_CHOKE2"].ShouldBe("=;+=CNy3)-D=zI6gRP2w\\$B@0K;Y]e^EFnCmx\\$Dx?;.9wf-rgk1BcTR0]JtY<S:b_");
        result.Parsed["DONT_CHOKE3"].ShouldBe("MUcKSGSY@HCON<1S_siWTP`DgS*Ug],mu]SkqI|7V2eOk9:>&fw;>HEwms`D8E2H");
        result.Parsed["DONT_CHOKE4"].ShouldBe("m]zjzfRItw2gs[2:{p{ugENyFw9m)tH6_VCQzer`*noVaI<vqa3?FZ9+6U;K#Bfd");
        result.Parsed["DONT_CHOKE5"].ShouldBe("#la__nK?IxNlQ%`5q&DpcZ>Munx=[1-AMgAcwmPkToxTaB?kgdF5y`A8m=Oa-B!)");
        result.Parsed["DONT_CHOKE6"].ShouldBe("xlC&*<j4J<d._<JKH0RBJV!4(ZQEN-+&!0p137<g*hdY2H4xk?/;KO1\\$(W{:Wc}Q");
        result.Parsed["DONT_CHOKE7"].ShouldBe("?\\$6)m*xhTVewc#NVVgxX%eBhJjoHYzpXFg=gzn[rWXPLj5UWj@z\\$/UDm8o79n/p%");
        result.Parsed["DONT_CHOKE8"].ShouldBe("@}:[4#g%[R-CFR});bY(Z[KcDQDsVn2_y4cSdU<Mjy!c^F`G<!Ks7]kbS]N1:bP:");
    }

    [Fact]
    public void TestExpandsDomainWithHost()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["HOST"].ShouldBe("something");
        result.Parsed["DOMAIN"].ShouldBe("https://something");
    }

    [Fact]
    public void TestDoesNotExpandSingleQuote()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["SINGLE_QUOTE"].ShouldBe("$BASIC");
    }

    [Fact]
    public void TestHandlesDeepNesting()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["DEEP8"]
            .ShouldBe("prefix5-prefix4-prefix3-prefix2-prefix1-basic-suffix1-suffix2-suffix3-suffix4-suffix5");
    }

    [Fact]
    public void TestHandlesSelfReferencing()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["EXPAND_SELF"].ShouldBe("");
        result.Parsed["DEEP_SELF"].ShouldBe("basic-bar");
        result.Parsed["DEEP_SELF_PRIOR"].ShouldBe("prefix2-foo-suffix2");
    }

    [Fact]
    public void TestHandlesProgressiveUpdating()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["PROGRESSIVE"].ShouldBe("first-second");
    }

    [Fact]
    public void TestSingleQuoteExpansionBugTestCases()
    {
        ConfigOptions options = new() { Path = [EnvExpandFileName] };
        ConfigResult result = Main.Config(options);

        // Case 1: Should work - first variable in single quotes
        result.Parsed["PGUSER"].ShouldBe("user");
        result.Parsed["PGHOST"].ShouldBe("localhost");
        result.Parsed["DATABASE_URL"].ShouldBe("postgres://user@localhost/my_database");

        // Case 2: Should work - first variable in single quotes, second in double quotes
        result.Parsed["PGUSER2"].ShouldBe("user");
        result.Parsed["PGHOST2"].ShouldBe("localhost");
        result.Parsed["DATABASE_URL2"].ShouldBe("postgres://user@localhost/my_database");

        // Case 3: Should work - first variable in double quotes
        result.Parsed["PGUSER3"].ShouldBe("user");
        result.Parsed["PGHOST3"].ShouldBe("localhost");
        result.Parsed["DATABASE_URL3"].ShouldBe("postgres://user@localhost/my_database");

        // Case 4: Should work - both variables in single quotes
        result.Parsed["PGUSER4"].ShouldBe("user");
        result.Parsed["PGHOST4"].ShouldBe("localhost");
        result.Parsed["DATABASE_URL4"].ShouldBe("postgres://user@localhost/my_database");

        // Case 5: Should work - both variables in double quotes
        result.Parsed["PGUSER5"].ShouldBe("user");
        result.Parsed["PGHOST5"].ShouldBe("localhost");
        result.Parsed["DATABASE_URL5"].ShouldBe("postgres://user@localhost/my_database");
    }
}