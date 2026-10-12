using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Helpers;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test;

/// <remarks>
/// Not from a JS test - commander silently drops EXTRA positional arguments but rejects an unknown OPTION
/// (`error: unknown option '--x'`); see UnknownOptionGuard.cs and trap #38.
/// </remarks>
public class UnknownOptionGuardTest : EnvironmentTestBase
{
    private RootCommand BuildRoot()
    {
        RootCommand root = new LoggingRootCommand { new GetAction(MockLogger), new RunAction(MockLogger) };
        foreach (Command command in root.Subcommands)
        {
            UnknownOptionGuard.Apply(command);
        }

        return root;
    }

    /// <remarks>Not from a JS test - an unrecognized option is reported, not bound as a positional value.</remarks>
    [Fact]
    public void TestUnknownOptionIsFound()
    {
        ParseResult result = BuildRoot().Parse(["get", "--totally-made-up-flag", "HELLO"]);

        UnknownOptionGuard.FindUnknownOption(result).ShouldBe("--totally-made-up-flag");
    }

    /// <remarks>Not from a JS test - extra positional values are tolerated (no parse error, nothing reported), like commander.</remarks>
    [Fact]
    public void TestExtraPositionalValueIsTolerated()
    {
        ParseResult result = BuildRoot().Parse(["get", "-f", ".env", ".env.prod", "HELLO"]);

        result.Errors.ShouldBeEmpty();
        UnknownOptionGuard.FindUnknownOption(result).ShouldBeNull();
    }

    /// <remarks>Not from a JS test - everything after `--` belongs to the wrapped command, dashes included.</remarks>
    [Fact]
    public void TestDashesAfterDoubleDashAreLeftAlone()
    {
        ParseResult result = BuildRoot().Parse(["run", "--", "node", "-e", "console.log(1)"]);

        result.Errors.ShouldBeEmpty();
        UnknownOptionGuard.FindUnknownOption(result).ShouldBeNull();
    }

    /// <remarks>Not from a JS test - known options (including `run`'s own `-e` before the child command) are not unknown.</remarks>
    [Fact]
    public void TestKnownOptionsAreNotFlagged()
    {
        UnknownOptionGuard.FindUnknownOption(BuildRoot().Parse(["run", "-e", "A=1", "--", "echo"])).ShouldBeNull();
        UnknownOptionGuard.FindUnknownOption(BuildRoot().Parse(["get", "-f", ".env", "--format", "shell"])).ShouldBeNull();
    }

    /// <remarks>Not from a JS test - dotenvx.js registers `set` with `.allowUnknownOption()`, so unknown options and dash-leading values pass there.</remarks>
    [Fact]
    public void TestSetAllowsUnknownOptions()
    {
        RootCommand root = new LoggingRootCommand { new SetAction(MockLogger) };
        foreach (Command command in root.Subcommands)
        {
            UnknownOptionGuard.Apply(command);
        }

        UnknownOptionGuard.FindUnknownOption(root.Parse(["set", "KEY", "-secret"])).ShouldBeNull();
        UnknownOptionGuard.FindUnknownOption(root.Parse(["set", "KEY", "value", "--whatever"])).ShouldBeNull();
    }

    /// <remarks>Not from a JS test - commander shows help even when an unknown option comes first.</remarks>
    [Fact]
    public void TestHelpWinsOverAnUnknownOption()
    {
        UnknownOptionGuard.FindUnknownOption(BuildRoot().Parse(["get", "--bogus", "--help"])).ShouldBeNull();
    }
}
