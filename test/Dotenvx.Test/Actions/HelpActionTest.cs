using System.CommandLine;

using Dotenvx.Core.Test;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/dotenvx.js (`help [command]`). Not from a JS test - upstream has none; behavior
/// verified live against the Node CLI: `help x` prints the same text as `x --help`, unknown names print the root help,
/// exit code 0.
/// </remarks>
public class HelpActionTest : EnvironmentTestBase
{
    private static string Invoke(RootCommand root, params string[] args)
    {
        var output = new StringWriter { NewLine = "\n" };
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            root.Parse(args).Invoke().ShouldBe(0);
        }
        finally
        {
            Console.SetOut(original);
        }

        return output.ToString();
    }

    private static RootCommand Root()
    {
        return new Dotenvx.LoggingRootCommand { new Dotenvx.Actions.HelpAction(), new Command("get", "get a value") };
    }

    [Fact]
    public void HelpOfACommandEqualsItsDashDashHelp()
    {
        Invoke(Root(), "help", "get").ShouldBe(Invoke(Root(), "get", "--help"));
    }

    [Theory]
    [InlineData("help")]
    [InlineData("help", "bogus")]
    public void HelpWithoutAKnownCommandPrintsRootHelp(params string[] args)
    {
        Invoke(Root(), args).ShouldBe(Invoke(Root(), "--help"));
    }
}
