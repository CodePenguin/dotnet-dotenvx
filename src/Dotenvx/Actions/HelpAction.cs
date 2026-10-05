using System.Linq;
using System.CommandLine;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/dotenvx.js (`program.command('help [command]')`): prints the named top-level
/// command's help, or the root help when the name is missing or unknown. Like commander, always exits 0.
/// </remarks>
public class HelpAction : Command
{
    private readonly Argument<string?> _commandArgument =
        new("command") { Arity = ArgumentArity.ZeroOrOne };

    public HelpAction() : base("help", "display help for command")
    {
        Add(_commandArgument);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        var root = (RootCommand)parseResult.RootCommandResult.Command;
        string? name = parseResult.GetValue(_commandArgument);
        bool known = name is not null && root.Subcommands.Any(command => command.Name == name);

        root.Parse(known ? [name!, "--help"] : ["--help"]).Invoke();

        return 0;
    }
}
