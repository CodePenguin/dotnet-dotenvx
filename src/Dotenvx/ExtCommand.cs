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
    public ExtCommand(
        GenexampleAction genexampleAction, GitignoreAction gitignoreAction, LsAction lsAction,
        PrebuildAction prebuildAction, PrecommitAction precommitAction, ScanAction scanAction)
        : base("ext", "🔌 extensions")
    {
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
