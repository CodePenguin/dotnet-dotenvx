using System.CommandLine;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/dotenvx.js `hidden` (a menu of hidden, beta and deprecated commands; its text is the help `after` trailer).</remarks>
public class HiddenCommand : Command
{
    public HiddenCommand() : base("hidden", "hidden features")
    {
        SetAction(parseResult =>
        {
            parseResult.RootCommandResult.Command.Parse(["hidden", "--help"]).Invoke();

            return 0;
        });
    }
}
