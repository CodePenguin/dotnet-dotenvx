using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test;

/// <remarks>
/// Reference source: dotenvx/src/lib/main.js `module.exports` (`ls`, `withEvents`, `getColor`, `bold`), exposed on the
/// <see cref="Dotenvx" /> class.
/// </remarks>
public class DotenvxFacadeTest : EnvironmentTestBase
{
    /// <remarks>Refer to main.js `ls`</remarks>
    [Fact]
    public void TestLsRunsTheLsService()
    {
        Ls mockLs = Substitute.For<Ls>("./", null!, null!);
        mockLs.Run().Returns(["a/.env"]);
        Ls.Factory = (_, _, _) => mockLs;

        Dotenvx.Ls().ShouldBe(["a/.env"]);
    }

    /// <remarks>Refer to main.js `withEvents`</remarks>
    [Fact]
    public void TestWithEventsReturnsAnSdkClient()
    {
        Dotenvx.WithEvents().ShouldNotBeNull();
    }

    /// <remarks>Refer to shared/colors.js `getColor` and `bold`</remarks>
    [Fact]
    public void TestGetColorAndBoldWrapTheMessage()
    {
        Dotenvx.GetColor("red", ColorStream.StdErr)("hello").ShouldContain("hello");
        Dotenvx.Bold("hello", ColorStream.StdOut).ShouldContain("hello");
    }

    /// <remarks>Refer to shared/colors.js `getColor` with a color it does not know</remarks>
    [Fact]
    public void TestGetColorRejectsAnUnknownColor()
    {
        Should.Throw<InvalidColorException>(() => Dotenvx.GetColor("not-a-color", ColorStream.StdOut));
    }
}
