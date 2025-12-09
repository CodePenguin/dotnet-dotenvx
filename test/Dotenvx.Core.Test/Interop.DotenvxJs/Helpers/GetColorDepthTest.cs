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
    }

    public void Dispose()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("TERM", null);
        Environment.SetEnvironmentVariable("COLORTERM", null);
    }

    /// <remarks>Refer to "falls back to process.env.TERM when getColorDepth throws an error (deno scenario)"</remarks>
    [Fact]
    public void TestFallsBackToTermWhenTermIsXterm256Color()
    {
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");

        int depth = GetColorDepth();

        depth.ShouldBe(8);
    }

    /// <remarks>Refer to "falls back to 4 when no process.env.TERM when getColorDepth throws an error (deno scenario)"</remarks>
    [Fact]
    public void TestFallsBackTo4WhenNoTerm()
    {
        int depth = GetColorDepth();

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

        int depth = GetColorDepth();

        depth.ShouldBe(4);
    }

    // Note: The first JS test stubs WriteStream.getColorDepth, which is not applicable in C# port.
    // The C# implementation directly uses TERM fallback logic.
}