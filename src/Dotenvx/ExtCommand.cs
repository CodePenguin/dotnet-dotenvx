using System.CommandLine;

using System.Collections.Generic;
using System.Linq;

using Dotenvx.Actions;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/ext.js. `ext <name> [args...]` for anything that is not one of the registered
/// subcommands (ls/genexample/gitignore/prebuild/precommit/scan) runs an external `dotenvx-ext-<name>` program
/// (executeExtension.js, see <see cref="ExecuteDynamic.Extension" />); `ext` alone prints its help and exits 0. Upstream's
/// helpInformation override that hides the dynamic "Arguments:" section is unnecessary here (System.CommandLine prints
/// the dynamic arguments only when they have a description).
/// </remarks>
public class ExtCommand : Command
{
    public ExtCommand(
        GenexampleAction genexampleAction, GitignoreAction gitignoreAction, LsAction lsAction,
        PrebuildAction prebuildAction, PrecommitAction precommitAction, ScanAction scanAction, IDotenvxLogger logger)
        : base("ext", "🔌 extensions")
    {
        var commandArgument = new Argument<string?>("command") { Arity = ArgumentArity.ZeroOrOne };
        var argsArgument = new Argument<string[]>("args") { Arity = ArgumentArity.ZeroOrMore };
        Add(commandArgument);
        Add(argsArgument);
        SetAction(parseResult =>
        {
            string? command = parseResult.GetValue(commandArgument);
            string[] rest = parseResult.GetValue(argsArgument) ?? [];
            string[] rawArgs = command is null ? [] : [command, .. rest];

            return ExecuteDynamic.Extension(command, rawArgs, logger,
                () => parseResult.RootCommandResult.Command.Parse(["ext", "--help"]).Invoke());
        });

        // Order matches ext.js's own registration order exactly (ls, genexample, gitignore,
        // prebuild, precommit, scan) - purely a `dotenvx ext --help` listing-order detail, but
        // upstream itself registers this same set in a DIFFERENT order at the top level
        // (gitignore before genexample, precommit before prebuild), so there's no single "right"
        // order to copy uniformly; this file specifically mirrors ext.js's own list.
        Subcommands.Add(lsAction);
        Subcommands.Add(genexampleAction);
        Subcommands.Add(gitignoreAction);
        Subcommands.Add(prebuildAction);
        Subcommands.Add(precommitAction);
        Subcommands.Add(scanAction);
    }
}
