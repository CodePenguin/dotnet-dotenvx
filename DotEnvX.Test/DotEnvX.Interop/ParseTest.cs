using dotenvx.Dotenvx.Interop;

using Shouldly;

namespace dotnet_dotenvx.Test.DotEnvX.Interop;

public class ParseTest
{
    [Fact]
    public void TestNormalizedLineEndings()
    {
        var expectedPayload = new Dictionary<string, string>
        {
            { "SERVER", "localhost" },
            { "PASSWORD", "password"},
            {"DB", "tests"}
        };

        var rPayload = Dotenvx.Parse("SERVER=localhost\rPASSWORD=password\rDB=tests\r");
        rPayload.ShouldBeEquivalentTo(expectedPayload, @"can parse (\r) line endings");

        var nPayload = Dotenvx.Parse("SERVER=localhost\nPASSWORD=password\nDB=tests\n");
        nPayload.ShouldBeEquivalentTo(expectedPayload, @"can parse (\n) line endings");

        var rnPayload = Dotenvx.Parse("SERVER=localhost\r\nPASSWORD=password\r\nDB=tests\r\n");
        rnPayload.ShouldBeEquivalentTo(expectedPayload, @"can parse (\r\n) line endings");
    }
}