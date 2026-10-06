using System.Linq;
using Dotenvx.Help;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;

using Dotenvx.Actions;
using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Logging;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx;

public class DotenvxRootCommand : LoggingRootCommand
{
    public DotenvxRootCommand(
        ArmorCommand armorCommand,
        CurlAction curlAction,
        DecryptAction decryptAction,
        DelAction delAction,
        DoctorAction doctorAction,
        EncryptAction encryptAction,
        ExtCommand extCommand,
        GenexampleAction genexampleAction,
        GetAction getAction,
        GitignoreAction gitignoreAction,
        global::Dotenvx.Actions.HelpAction helpAction,
        KeyPairAction keyPairAction,
        LockCommand lockCommand,
        LoginAction loginAction,
        LogoutAction logoutAction,
        LsAction lsAction,
        NativeCommand nativeCommand,
        PrebuildAction prebuildAction,
        PrecommitAction precommitAction,
        RunAction runAction,
        SetAction setAction,
        UpdateAction updateAction,
        ValidateAction validateAction
    ) : base("a secure dotenv–from the creator of `dotenv`")
    {
        UpdateBuiltInCommands([extCommand, lockCommand, doctorAction, nativeCommand, armorCommand, curlAction, loginAction, logoutAction]);

        // Add Actions
        Subcommands.Add(armorCommand);
        Subcommands.Add(curlAction);
        Subcommands.Add(loginAction);
        Subcommands.Add(logoutAction);
        Subcommands.Add(decryptAction);
        Subcommands.Add(delAction);
        Subcommands.Add(doctorAction);
        Subcommands.Add(encryptAction);
        Subcommands.Add(genexampleAction);
        Subcommands.Add(getAction);
        Subcommands.Add(gitignoreAction);
        Subcommands.Add(helpAction);
        Subcommands.Add(keyPairAction);
        Subcommands.Add(lockCommand);
        Subcommands.Add(lsAction);
        Subcommands.Add(nativeCommand);
        Subcommands.Add(prebuildAction);
        Subcommands.Add(precommitAction);
        Subcommands.Add(runAction);
        Subcommands.Add(validateAction);
        Subcommands.Add(setAction);
        Subcommands.Add(updateAction);
        Subcommands.Add(extCommand);

        foreach (Command command in Subcommands)
        {
            UnknownOptionGuard.Apply(command);
        }
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
    private sealed class FilteringHelpAction(
        SynchronousCommandLineAction inner, Command root, IReadOnlyList<Command> hideFromRootOnly)
        : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            bool isRootHelp = parseResult.CommandResult.Command == root;

            // commands that exist upstream print commander's exact layout; ours without an upstream twin keep the default
            // (DOTENVX_NATIVE_HELP=1 asks for System.CommandLine's own text: HelpTextParityTest checks the real option definitions)
            HelpSpec? spec = Environment.GetEnvironmentVariable("DOTENVX_NATIVE_HELP") == "1"
                ? null
                : CommanderHelp.Find(CommanderErrors.PathOf(parseResult));
            if (spec is not null)
            {
                Console.Out.Write(CommanderHelp.Render(spec, isRootHelp));
                WriteTrailer(parseResult.CommandResult.Command, isRootHelp);

                return 0;
            }

            if (isRootHelp)
            {
                foreach (Command command in hideFromRootOnly)
                {
                    command.Hidden = true;
                }
            }

            try
            {
                int exitCode = inner.Invoke(parseResult);
                WriteTrailer(parseResult.CommandResult.Command, isRootHelp);

                return exitCode;
            }
            finally
            {
                if (isRootHelp)
                {
                    foreach (Command command in hideFromRootOnly)
                    {
                        command.Hidden = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// `addHelpText('after', ...)`: the "Examples:" trailers of run/set/del/gitignore/precommit/prebuild/curl and the
    /// "Better Security" / "For Security Teams" lines after the root help (each text is written followed by a newline).
    /// </summary>
    private static void WriteTrailer(Command command, bool isRoot)
    {
        if (isRoot)
        {
            foreach (string line in new[]
                     {
                         " ", "Better Security:",
                         "  lock                     ⊡ lock private keys with a local passphrase",
                         "  native                   ⌥ move private keys into your OS secret store", " ",
                         "For Security Teams:",
                         "  armor                    ⛨ move private keys into Dotenvx Armor [www.dotenvx.com/armor]",
                         "  curl                     ⛨ call authenticated api Dotenvx Armor [www.dotenvx.com/armor]"
                     })
            {
                Console.Out.Write(line + (char)10);
            }

            return;
        }

        string? examples = command.Name switch
        {
            "run" => HelpExamples.Run(),
            "set" => HelpExamples.Set(),
            "del" => HelpExamples.Del(),
            "gitignore" => HelpExamples.Gitignore(),
            "precommit" => HelpExamples.Precommit(),
            "prebuild" => HelpExamples.Prebuild(),
            "curl" => HelpExamples.Curl(),
            _ => null
        };

        if (examples is not null)
        {
            Console.Out.Write(examples + (char)10);
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

    private void UpdateBuiltInCommands(IReadOnlyList<Command> hideFromRootOnly)
    {
        // Add alias for Version
        foreach (Option option in Options)
        {
            switch (option)
            {
                case HelpOption help:
                    option.Description = "display help for command";

                    // commander's help flags are only -h/--help; System.CommandLine adds -?, /h and /? (so `get -?` printed
                    // help instead of `error: unknown option '-?'`)
                    foreach (string alias in option.Aliases.Where(a => a != "-h").ToList())
                    {
                        option.Aliases.Remove(alias);
                    }

                    if (help.Action is SynchronousCommandLineAction defaultHelpAction)
                    {
                        help.Action = new FilteringHelpAction(defaultHelpAction, this, hideFromRootOnly);
                    }

                    break;
                case VersionOption:
                    option.Aliases.Add("-V");
                    option.Description = "output the version number";
                    // Recursive = true so `dotenvx <command> --version` also works, matching
                    // commander's own recursive option behavior - same fix shape as
                    // LoggingRootCommand's -l/-q/-v/-d (see docs/porting-traps.md, trap on this).
                    option.Recursive = true;
                    break;
            }
        }
    }
}