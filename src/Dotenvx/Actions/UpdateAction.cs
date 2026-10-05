using System;
using System.CommandLine;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/update.js. Upstream runs the `curl ... | sh` installer only when packaged
/// as a pkg binary (`process.pkg`); this port is never that, so it prints the manual npm commands like `node`
/// does.
/// </remarks>
public class UpdateAction : Command
{
    public UpdateAction() : base("update", "update dotenvx")
    {
        SetAction(Execute);
    }

    private static int Execute(ParseResult parseResult)
    {
        Console.WriteLine("global: npm install -g @dotenvx/dotenvx@latest");
        Console.WriteLine("local:  npm install @dotenvx/dotenvx@latest --save");
        return 0;
    }
}
