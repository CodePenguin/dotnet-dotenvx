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

    [Theory]
    [InlineData(new[] { "foo", "get" }, "foo")]
    [InlineData(new[] { "-q", "foo", "help" }, "foo")]
    [InlineData(new[] { "foo", "-f", "x" }, "foo")]
    [InlineData(new[] { "get", "foo" }, null)]
    [InlineData(new[] { "-f", "x" }, null)]
    [InlineData(new string[0], null)]
    public void FirstUnknownRootArgumentIsTheExtension(string[] args, string? expected)
    {
        CommanderArgs.Normalize(args, out _, out string? unknown);

        unknown.ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { "genexample", "-f=plain", "zzz" }, new[] { "genexample", "zzz", "-f", "=plain" })]
    [InlineData(new[] { "genexample", "-fplain", "zzz" }, new[] { "genexample", "zzz", "-f", "plain" })]
    [InlineData(new[] { "genexample", "-f", "plain", "-fk", "zzz" }, new[] { "genexample", "zzz", "-f", "plain", "-f", "k" })]
    [InlineData(new[] { "genexample", "d", "-f=plain", "z" }, new[] { "genexample", "d", "z", "-f", "=plain" })]
    [InlineData(new[] { "genexample", "-f", "plain", "zzz" }, new[] { "genexample", "-f", "plain", "zzz" })]
    public void AttachedValueEndsAVariadicOption(string[] args, string[] expected)
    {
        CommanderArgs.Normalize(args).ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { "get", "-fk=x" }, new[] { "get", "-f", "k=x" }, null)]
    [InlineData(new[] { "get", "-fk:x" }, new[] { "get", "-f", "k:x" }, null)]
    [InlineData(new[] { "get", "-ik=A" }, new[] { "get", "-ik=A" }, "-ik=A")]
    [InlineData(new[] { "ext", "ls", "-ef=x" }, new[] { "ext", "ls", "-ef=x" }, "-ef=x")]
    [InlineData(new[] { "get", "-f=x" }, new[] { "get", "-f", "=x" }, null)]
    public void MultiLetterShortFlagsWithAnAttachedValue(string[] args, string[] expected, string? unknownShort)
    {
        CommanderArgs.Normalize(args, out _, out _, out string? unknown).ShouldBe(expected);
        unknown.ShouldBe(unknownShort);
    }
}
