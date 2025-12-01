using System.CommandLine;
using System.CommandLine.Help;
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
        EncryptAction encryptAction,
        GetAction getAction,
        KeyPairAction keyPairAction,
        LsAction lsAction,
        RotateAction rotateAction,
        RunAction runAction,
        SetAction setAction
    ) : base("a secure dotenv–from the creator of `dotenv`")
    {
        UpdateBuiltInCommands();

        // Add Actions
        Subcommands.Add(decryptAction);
        Subcommands.Add(encryptAction);
        Subcommands.Add(getAction);
        Subcommands.Add(keyPairAction);
        Subcommands.Add(lsAction);
        Subcommands.Add(rotateAction);
        Subcommands.Add(runAction);
        Subcommands.Add(setAction);
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

    private void UpdateBuiltInCommands()
    {
        // Add alias for Version
        foreach (Option option in Options)
        {
            switch (option)
            {
                case HelpOption:
                    option.Description = "display help for command";
                    break;
                case VersionOption:
                    option.Aliases.Add("-V");
                    option.Description = "output the version number";
                    break;
            }
        }
    }
}