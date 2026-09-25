using Dotenvx.Interop.DotenvxJs.Shared;

using Shouldly;

using Colors = Dotenvx.Interop.DotenvxJs.Shared.Shared;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Shared;

/// <remarks>Reference source: dotenvx/tests/shared/colors.test.js</remarks>
public class ColorsTest : EnvironmentTestBase
{
    public ColorsTest()
    {
        ConsoleTty.IsStdOutTty = () => true;
        ConsoleTty.IsStdErrTty = () => true;
        // The dev/CI shell's own TERM/COLORTERM (e.g. git-bash's "xterm-256color"/"truecolor")
        // would otherwise leak into every depth calculation below - clear them so each test
        // controls its own color-depth scenario deterministically, matching ConventionsTest.cs's
        // established pattern for the same class of ambient-env-var leakage.
        Environment.SetEnvironmentVariable("TERM", null);
        Environment.SetEnvironmentVariable("COLORTERM", null);
    }

    /// <remarks>Refer to "getColor with ansi256 color support"</remarks>
    [Fact]
    public void TestGetColorWithAnsi256ColorSupport()
    {
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");

        Colors.GetColor("orangered", ColorStream.StdErr)("hello")
            .ShouldBe("\x1b[38;5;130mhello\x1b[39m");
    }

    /// <remarks>Refer to "getColor with truecolor support"</remarks>
    [Fact]
    public void TestGetColorWithTrueColorSupport()
    {
        // TERM must be non-empty and not itself match the 256color/xterm/truecolor/24bit patterns,
        // or GetColorDepth's TERM branch resolves the depth before ever consulting COLORTERM -
        // see GetColorDepth.cs's fallback order (an already-established, pre-existing shape, not
        // something this test changes).
        Environment.SetEnvironmentVariable("TERM", "screen");
        Environment.SetEnvironmentVariable("COLORTERM", "truecolor");

        Colors.GetColor("amber", ColorStream.StdErr)("hello")
            .ShouldBe("\x1b[38;2;236;213;63mhello\x1b[39m");
    }

    /// <remarks>Refer to "getColor with ansi16 color support"</remarks>
    [Fact]
    public void TestGetColorWithAnsi16ColorSupport()
    {
        Environment.SetEnvironmentVariable("TERM", "not-a-known-term");

        Colors.GetColor("orangered", ColorStream.StdErr)("hello")
            .ShouldBe("\x1b[33mhello\x1b[39m");
    }

    /// <remarks>Refer to "getColor without color support"</remarks>
    [Fact]
    public void TestGetColorWithoutColorSupport()
    {
        ConsoleTty.IsStdErrTty = () => false;

        Colors.GetColor("orangered", ColorStream.StdErr)("hello").ShouldBe("hello");
    }

    /// <remarks>
    /// Refer to "getColor passes the destination stream to color depth detection". This port has no
    /// single injectable getColorDepth stub to assert a call against directly (see
    /// ConsoleTty.cs/GetColorDepthTest.cs) - proven instead by giving stdout and stderr genuinely
    /// different TTY states and confirming GetColor's result differs accordingly per stream.
    /// </remarks>
    [Fact]
    public void TestGetColorPassesTheDestinationStreamToColorDepthDetection()
    {
        ConsoleTty.IsStdOutTty = () => false;
        ConsoleTty.IsStdErrTty = () => true;
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");

        Colors.GetColor("gray", ColorStream.StdOut)("hello").ShouldBe("hello");
        Colors.GetColor("gray", ColorStream.StdErr)("hello")
            .ShouldBe("\x1b[38;5;244mhello\x1b[39m");
    }

    /// <remarks>Refer to "getColor invalid color"</remarks>
    [Fact]
    public void TestGetColorInvalidColor()
    {
        Should.Throw<ArgumentException>(() => Colors.GetColor("invalid", ColorStream.StdErr));
    }

    /// <remarks>Refer to "bold with ansi16 color support"</remarks>
    [Fact]
    public void TestBoldWithAnsi16ColorSupport()
    {
        Environment.SetEnvironmentVariable("TERM", "not-a-known-term");

        Colors.Bold("hello", ColorStream.StdErr).ShouldBe("\x1b[1mhello\x1b[22m");
    }

    /// <remarks>Refer to "bold without color support"</remarks>
    [Fact]
    public void TestBoldWithoutColorSupport()
    {
        ConsoleTty.IsStdErrTty = () => false;

        Colors.Bold("hello", ColorStream.StdErr).ShouldBe("hello");
    }
}
