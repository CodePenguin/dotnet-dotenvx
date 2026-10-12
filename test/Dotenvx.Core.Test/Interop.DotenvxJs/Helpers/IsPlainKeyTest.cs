using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/cryptography/isPlainKey.js</remarks>
public class IsPlainKeyTest
{
    [Fact]
    public void KeyEndingInPlainIsPlain()
    {
        IsPlainKey("HELLO_PLAIN").ShouldBeTrue();
    }

    [Fact]
    public void KeyNotEndingInPlainIsNotPlain()
    {
        IsPlainKey("HELLO").ShouldBeFalse();
    }

    [Fact]
    public void KeyContainingButNotEndingInPlainIsNotPlain()
    {
        IsPlainKey("PLAIN_HELLO").ShouldBeFalse();
    }
}
