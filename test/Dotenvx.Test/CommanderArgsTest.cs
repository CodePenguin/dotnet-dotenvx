using Dotenvx.Help;

using Shouldly;

namespace Dotenvx.Test;

/// <remarks>Not from a JS test - commander's parseOptions rules (bundled in @dotenvx/tooling) as normalized by CommanderArgs.</remarks>
public class CommanderArgsTest
{
    [Theory]
    [InlineData(new[] { "run", "-e", "--x", "--", "node" }, new[] { "run", "-e=--x", "--", "node" })]
    [InlineData(new[] { "get", "HELLO", "-f", "--overload" }, new[] { "get", "HELLO", "-f=--overload" })]
    [InlineData(new[] { "get", "HELLO", "-f=.env" }, new[] { "get", "HELLO", "-f", "=.env" })]
    [InlineData(new[] { "-l", "--quiet", "get" }, new[] { "-l=--quiet", "get" })]
    public void RequiredValuesAndShortSeparators(string[] args, string[] expected)
    {
        CommanderArgs.Normalize(args).ShouldBe(expected);
    }
}
