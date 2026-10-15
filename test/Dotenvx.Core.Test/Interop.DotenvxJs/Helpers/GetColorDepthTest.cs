using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/colorDepth.test.js. Upstream stubs the stream's own probe; this port ports the
/// probe itself (Node 22.20's tty getColorDepth), so the table below is what that Node returns for each environment on a
/// non-Windows platform (generated with tty.WriteStream.prototype.getColorDepth(env)).
/// </remarks>
public class GetColorDepthTest : IDisposable
{
    private static readonly OsPlatform Linux = new(false, 0, 0);

    public GetColorDepthTest()
    {
        ConsoleTty.IsStdOutTty = () => true;
        ConsoleTty.IsStdErrTty = () => true;
    }

    public void Dispose()
    {
        ConsoleTty.ResetToDefaults();
    }

    public static IEnumerable<object[]> Environments()
    {
        (Dictionary<string, string> Env, int Depth)[] table =
        [
            (new(), 1),
            (new() { ["FORCE_COLOR"] = "" }, 4),
            (new() { ["FORCE_COLOR"] = "1" }, 4),
            (new() { ["FORCE_COLOR"] = "true" }, 4),
            (new() { ["FORCE_COLOR"] = "2" }, 8),
            (new() { ["FORCE_COLOR"] = "3" }, 24),
            (new() { ["FORCE_COLOR"] = "0" }, 1),
            (new() { ["NO_COLOR"] = "1" }, 1),
            (new() { ["NO_COLOR"] = "" }, 1),
            (new() { ["NODE_DISABLE_COLORS"] = "1" }, 1),
            (new() { ["TERM"] = "dumb" }, 1),
            (new() { ["TMUX"] = "/tmp/x" }, 24),
            (new() { ["CI"] = "1" }, 1),
            (new() { ["CI"] = "1", ["GITHUB_ACTIONS"] = "true" }, 24),
            (new() { ["CI"] = "1", ["CI_NAME"] = "codeship" }, 8),
            (new() { ["CI"] = "" }, 1),
            (new() { ["TEAMCITY_VERSION"] = "9.0.1" }, 1),
            (new() { ["TEAMCITY_VERSION"] = "8.0.0" }, 1),
            (new() { ["TEAMCITY_VERSION"] = "2020.1.1" }, 4),
            (new() { ["TERM_PROGRAM"] = "iTerm.app" }, 8),
            (new() { ["TERM_PROGRAM"] = "iTerm.app", ["TERM_PROGRAM_VERSION"] = "2.9" }, 8),
            (new() { ["TERM_PROGRAM"] = "iTerm.app", ["TERM_PROGRAM_VERSION"] = "3.4.1" }, 24),
            (new() { ["TERM_PROGRAM"] = "HyperTerm" }, 24),
            (new() { ["TERM_PROGRAM"] = "MacTerm" }, 24),
            (new() { ["TERM_PROGRAM"] = "Apple_Terminal" }, 8),
            (new() { ["COLORTERM"] = "truecolor" }, 24),
            (new() { ["COLORTERM"] = "24bit" }, 24),
            (new() { ["COLORTERM"] = "yes" }, 4),
            (new() { ["COLORTERM"] = "" }, 1),
            (new() { ["TERM"] = "xterm-256color" }, 8),
            (new() { ["TERM"] = "xterm" }, 4),
            (new() { ["TERM"] = "XTERM-COLOR" }, 4),
            (new() { ["TERM"] = "eterm" }, 4),
            (new() { ["TERM"] = "mosh" }, 24),
            (new() { ["TERM"] = "st" }, 4),
            (new() { ["TERM"] = "xterm-kitty" }, 24),
            (new() { ["TERM"] = "screen" }, 4),
            (new() { ["TERM"] = "linux" }, 4),
            (new() { ["TERM"] = "vt100" }, 4),
            (new() { ["TERM"] = "con80x25" }, 4),
            (new() { ["TERM"] = "rxvt-unicode-24bit" }, 24),
            (new() { ["TERM"] = "something else" }, 1),
            (new() { ["TERM"] = "xterm-truecolor" }, 24),
            (new() { ["TERM"] = "xterm-direct" }, 4),
            (new() { ["TF_BUILD"] = "1", ["AGENT_NAME"] = "a" }, 4),
            (new() { ["TF_BUILD"] = "1" }, 1),
            (new() { ["CI"] = "1", ["TRAVIS"] = "1" }, 8),
            (new() { ["CI"] = "1", ["CIRCLECI"] = "1" }, 24),
            (new() { ["FORCE_COLOR"] = "2", ["NO_COLOR"] = "1" }, 8),
            (new() { ["NODE_DISABLE_COLORS"] = "" }, 1),
            (new() { ["TERM"] = "something else", ["COLORTERM"] = "yes" }, 4),
            (new() { ["TERM"] = "xterm", ["COLORTERM"] = "truecolor" }, 24),
        ];

        return table.Select(row => new object[] { row.Env, row.Depth });
    }

    /// <remarks>Refer to "returns the color depth of a TTY stream"</remarks>
    [Theory]
    [MemberData(nameof(Environments))]
    public void TestMatchesNodesColorDepth(Dictionary<string, string> env, int expected)
    {
        NodeColorDepth(name => env.GetValueOrDefault(name), Linux).ShouldBe(expected);
    }

    /// <remarks>Refer to "returns no color for a redirected stream"</remarks>
    [Fact]
    public void TestReturnsNoColorForARedirectedStream()
    {
        ConsoleTty.IsStdErrTty = () => false;

        GetColorDepth(ColorStream.StdErr).ShouldBe(1);
    }

    /// <summary>Node's Windows branch: build 14931 is the first with true colour, 10586 the first with 256 colours.</summary>
    [Theory]
    [InlineData(10, 19045, 24)]
    [InlineData(10, 14931, 24)]
    [InlineData(10, 14000, 8)]
    [InlineData(10, 10586, 8)]
    [InlineData(10, 10000, 4)]
    [InlineData(6, 9600, 4)]
    public void TestWindowsDependsOnTheBuild(int major, int build, int expected)
    {
        NodeColorDepth(_ => null, new OsPlatform(true, major, build)).ShouldBe(expected);
    }

    /// <summary>The Windows check comes after FORCE_COLOR / NO_COLOR / TERM=dumb and before everything else.</summary>
    [Fact]
    public void TestWindowsStillHonoursNoColor()
    {
        NodeColorDepth(name => name == "NO_COLOR" ? "1" : null, new OsPlatform(true, 10, 19045)).ShouldBe(1);
    }
}
