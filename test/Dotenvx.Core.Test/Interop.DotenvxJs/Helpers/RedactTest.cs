using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/redact.js</remarks>
public class RedactTest
{
    [Fact]
    public void RedactsNonEmptyString()
    {
        Redact("secret").ShouldBe("[REDACTED]");
    }

    [Fact]
    public void EmptyStringStaysEmpty()
    {
        Redact("").ShouldBe("");
    }

    [Fact]
    public void NullStaysEmpty()
    {
        Redact(null).ShouldBe("");
    }
}
