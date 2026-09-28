using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;

using Dotenvx.Actions;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Logging;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx;

public class DotenvxRootCommand : LoggingRootCommand
{
    public DotenvxRootCommand(
        DecryptAction decryptAction,
        DelAction delAction,
        EncryptAction encryptAction,
        ExtCommand extCommand,
        GenexampleAction genexampleAction,
        GetAction getAction,
        GitignoreAction gitignoreAction,
        KeyPairAction keyPairAction,
        LsAction lsAction,
        PrebuildAction prebuildAction,
        PrecommitAction precommitAction,
        RotateAction rotateAction,
        RunAction runAction,
        SetAction setAction,
        ValidateAction validateAction
    ) : base("a secure dotenv–from the creator of `dotenv`")
    {
        UpdateBuiltInCommands(extCommand);

        // Add Actions
        Subcommands.Add(decryptAction);
        Subcommands.Add(delAction);
        Subcommands.Add(encryptAction);
        Subcommands.Add(genexampleAction);
        Subcommands.Add(getAction);
        Subcommands.Add(gitignoreAction);
        Subcommands.Add(keyPairAction);
        Subcommands.Add(lsAction);
        Subcommands.Add(prebuildAction);
        Subcommands.Add(precommitAction);
        Subcommands.Add(rotateAction);
        Subcommands.Add(runAction);
        Subcommands.Add(validateAction);
        Subcommands.Add(setAction);
        Subcommands.Add(extCommand);
    }

    /// <remarks>
    /// dotenvx.js's program.helpInformation override strips any line containing "🔌 extensions"
    /// from `dotenvx --help`'s command list, while `dotenvx ext --help` on its own still shows
    /// normally. System.CommandLine 2.0.0's Command.Hidden can't reproduce this directly - it's a
    /// single flag on the Command object itself, checked BOTH by a parent's subcommand-listing
    /// filter AND by HelpBuilder.Write's own "if (context.Command.Hidden) return;" early exit -
    /// so marking extCommand.Hidden permanently true hides it from the root's listing but also
    /// makes `dotenvx ext --help` render nothing at all (confirmed empirically). This wrapper
    /// toggles Hidden only for the single Invoke() call where the root itself is the help target,
    /// restoring it immediately after - `dotenvx ext --help`'s own CommandResult.Command is
    /// extCommand, not the root, so it's untouched.
    /// </remarks>
    private sealed class FilteringHelpAction(SynchronousCommandLineAction inner, Command root, Command hideFromRootOnly)
        : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            bool isRootHelp = parseResult.CommandResult.Command == root;
            if (isRootHelp)
            {
                hideFromRootOnly.Hidden = true;
            }

            try
            {
                return inner.Invoke(parseResult);
            }
            finally
            {
                if (isRootHelp)
                {
                    hideFromRootOnly.Hidden = false;
                }
            }
        }
    }

    public static DotenvxLoggerOptions GetDotenvxLoggerConfiguration(string[] args)
    {
        string logLevel = "info";

        LoggingRootCommand rootCommand = new() { TreatUnmatchedTokensAsErrors = false };
        rootCommand.SetAction(parseResult =>
        {
            logLevel = rootCommand.GetLogLevel(parseResult);
        });

        ParseResult parseResult = rootCommand.Parse(args);
        parseResult.Invoke(new InvocationConfiguration { Error = TextWriter.Null, Output = TextWriter.Null });

        return new DotenvxLoggerOptions { LogLevel = DotenvxLogLevelExtensions.FromString(logLevel) };
    }

    private void UpdateBuiltInCommands(ExtCommand extCommand)
    {
        // Add alias for Version
        foreach (Option option in Options)
        {
            switch (option)
            {
                case HelpOption help:
                    option.Description = "display help for command";
                    if (help.Action is SynchronousCommandLineAction defaultHelpAction)
                    {
                        help.Action = new FilteringHelpAction(defaultHelpAction, this, extCommand);
                    }

                    break;
                case VersionOption:
                    option.Aliases.Add("-V");
                    option.Description = "output the version number";
                    // Recursive = true so `dotenvx <command> --version` also works, matching
                    // commander's own recursive option behavior - same fix shape as
                    // LoggingRootCommand's -l/-q/-v/-d (see CLAUDE.md's trap on this).
                    option.Recursive = true;
                    break;
            }
        }
    }
}