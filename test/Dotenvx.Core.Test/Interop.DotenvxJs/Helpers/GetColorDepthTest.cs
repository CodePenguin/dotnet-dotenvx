using Dotenvx.Interop.DotenvxJs.Shared;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/colorDepth.test.js</remarks>
public class GetColorDepthTest : IDisposable
{
    public GetColorDepthTest()
    {
        // Clear environment variables before each test
        Environment.SetEnvironmentVariable("TERM", null);
        Environment.SetEnvironmentVariable("COLORTERM", null);
        // Simulate a real terminal by default - most scenarios here are about the TERM/COLORTERM
        // fallback, which only runs once a stream IS a TTY (see TestReturnsNoColorForARedirectedStream
        // for the opposite case). ConsoleTty can't be forced via real console redirection in a test
        // runner (which always redirects both streams), so it's swapped directly instead.
        ConsoleTty.IsStdOutTty = () => true;
        ConsoleTty.IsStdErrTty = () => true;
    }

    public void Dispose()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("TERM", null);
        Environment.SetEnvironmentVariable("COLORTERM", null);
        ConsoleTty.ResetToDefaults();
    }

    /// <remarks>Refer to "returns no color for a redirected stream"</remarks>
    [Fact]
    public void TestReturnsNoColorForARedirectedStream()
    {
        ConsoleTty.IsStdErrTty = () => false;

        int depth = GetColorDepth(ColorStream.StdErr);

        depth.ShouldBe(1);
    }

    /// <remarks>
    /// Refer to "returns the color depth of a TTY stream". Upstream stubs a real per-stream
    /// `getColorDepth()` method returning 8 directly; this port's already-accepted simplification
    /// (see GetColorDepth.cs's doc comment) has no such per-stream probe and falls through to the
    /// same TERM/COLORTERM logic as the no-stream case once it's confirmed to be a real TTY - this
    /// just confirms that fallthrough actually happens for BOTH streams, not just stderr.
    /// </remarks>
    [Fact]
    public void TestReturnsTheColorDepthOfATtyStream()
    {
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");

        GetColorDepth(ColorStream.StdOut).ShouldBe(8);
        GetColorDepth(ColorStream.StdErr).ShouldBe(8);
    }

    /// <remarks>Refer to "falls back to process.env.TERM when getColorDepth throws an error (deno scenario)"</remarks>
    [Fact]
    public void TestFallsBackToTermWhenTermIsXterm256Color()
    {
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");

        int depth = GetColorDepth(ColorStream.StdErr);

        depth.ShouldBe(8);
    }

    /// <remarks>Refer to "falls back to 4 when no process.env.TERM when getColorDepth throws an error (deno scenario)"</remarks>
    [Fact]
    public void TestFallsBackTo4WhenNoTerm()
    {
        int depth = GetColorDepth(ColorStream.StdErr);

        depth.ShouldBe(4);
    }

    /// <remarks>
    ///     Refer to "falls back to 4 when process.env.TERM neither xterm or 256color when getColorDepth throws an error
    ///     (deno scenario)"
    /// </remarks>
    [Fact]
    public void TestFallsBackTo4WhenTermNotMatching()
    {
        Environment.SetEnvironmentVariable("TERM", "something else");

        int depth = GetColorDepth(ColorStream.StdErr);

        depth.ShouldBe(4);
    }

    // Note: The first JS test stubs WriteStream.getColorDepth, which is not applicable in C# port.
    // The C# implementation directly uses TERM fallback logic.
}
