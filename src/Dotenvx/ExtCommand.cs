using System.CommandLine;

using Dotenvx.Actions;

namespace Dotenvx;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/ext.js. Upstream implements this as a dynamic-
/// dispatch shim (a catch-all [command] argument routed through executeExtension.js, which
/// spawns external `dotenvx-ext-&lt;name&gt;` binaries for anything not explicitly registered below,
/// e.g. `dotenvx ext vault`) plus a custom helpInformation override to hide the dynamically-
/// dispatched entries from --help. This port deliberately omits that dynamic-loading mechanism -
/// every subcommand `ext` actually ships with upstream (ls/gitignore/genexample/prebuild/
/// precommit/scan) is registered explicitly below as a real nested Command, which is simpler and
/// idiomatic for System.CommandLine. The one user-visible difference: `dotenvx ext vault` (an
/// unregistered external extension) gets System.CommandLine's generic "unrecognized command"
/// error here, instead of upstream's vault-specific install hint.
/// </remarks>
public class ExtCommand : Command
{
    public ExtCommand(GenexampleAction genexampleAction, GitignoreAction gitignoreAction, LsAction lsAction)
        : base("ext", "🔌 extensions")
    {
        Subcommands.Add(lsAction);
        Subcommands.Add(gitignoreAction);
        Subcommands.Add(genexampleAction);
    }
}
