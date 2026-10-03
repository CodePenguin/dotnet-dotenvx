using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/redact.test.js</remarks>
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

    /// <remarks>Refer to "redact replaces strings of any length"</remarks>
    [Fact]
    public void ReplacesStringsOfAnyLength()
    {
        Redact("a").ShouldBe("[REDACTED]");
        Redact("super-secret-value").ShouldBe("[REDACTED]");
        Redact("").ShouldBe("");
    }
}
