using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

using Dotenvx.Help;
using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dotenvx.Extensions;

public static class HostExtensions
{
    public static int RunDotenvxCommands(this IHost host, string[] args)
    {
        DotenvxRootCommand root = host.Services.GetRequiredService<DotenvxRootCommand>();

        // commander prints the help and exits 1 when dotenvx is run with no arguments
        if (args.Length == 0)
        {
            root.Parse(["--help"]).Invoke();
            return 1;
        }

        string[] normalized = CommanderArgs.Normalize(args, out string? missingTerm, out string? firstUnknown, out string? unknownShort);

        // an unknown first argument is the extension to run, whatever follows it (`dotenvx foo get` runs `dotenvx-foo get`);
        // commander still handles -h/--help/-V/--version anywhere before the action runs
        if (firstUnknown is not null && !args.TakeWhile(a => a != "--").Any(a => a is "-h" or "--help" or "-V" or "--version"))
        {
            // commander consumes the root options after the unknown command too: `foo -l` is `option '-l, --log-level <level>' argument missing`
            if (missingTerm is not null)
            {
                Console.Error.WriteLine($"error: option '{missingTerm}' argument missing");
                return 1;
            }

            // `process.argv.slice(3)`: everything after the first user argument
            AnnounceLogLevel(host);
            return ExecuteDynamic.Dynamic(firstUnknown, args[1..], host.Services.GetRequiredService<IDotenvxLogger>());
        }

        ParseResult parseResult = root.Parse(normalized);

        // dotenvx.js: the root's `[command] [args...]` action runs executeDynamic: `dotenvx-<command>` from PATH, else
        // `error: unknown command '<command>'` on stdout and exit 0
        // (commander handles -h/--help before the action runs, so `dotenvx foo --help` prints the root help and exits 0)
        if (ReferenceEquals(parseResult.CommandResult.Command, root) && !RequestsHelpOrVersion(parseResult) &&
            parseResult.UnmatchedTokens.FirstOrDefault() is { } unknownCommand)
        {
            // `process.argv.slice(3)`: everything after the first user argument
            AnnounceLogLevel(host);
            return ExecuteDynamic.Dynamic(unknownCommand, args[1..], host.Services.GetRequiredService<IDotenvxLogger>());
        }

        // commander: `error: option '-f, --env-file <path>' argument missing` (stderr, exit 1)
        if (!RequestsHelpOrVersion(parseResult) && missingTerm is not null)
        {
            Console.Error.WriteLine($"error: option '{missingTerm}' argument missing");
            return 1;
        }

        // commander checks the required options before it looks at unknown ones
        if (!RequestsHelpOrVersion(parseResult) && CommanderErrors.RequiredOption(parseResult) is { } required)
        {
            Console.Error.WriteLine(required);
            return 1;
        }

        // commander rejects unknown options even though it tolerates extra positional values (see UnknownOptionGuard)
        if (UnknownOptionGuard.FindUnknownOption(parseResult) is { } unknownOption)
        {
            Console.Error.WriteLine(CommanderErrors.UnknownOption(unknownOption, CommanderErrors.PathOf(parseResult)));
            return 1;
        }

        // commander has no `--long:value` form: it is an unknown option (System.CommandLine would accept it)
        if (!RequestsHelpOrVersion(parseResult) && !UnknownOptionGuard.AcceptsUnknownOptions(parseResult) &&
            args.TakeWhile(a => a != "--").FirstOrDefault(a => ColonLongOption.IsMatch(a)) is { } colonOption)
        {
            Console.Error.WriteLine(CommanderErrors.UnknownOption(colonOption, CommanderErrors.PathOf(parseResult)));
            return 1;
        }

        // commander splits `-f=x` only for a single-letter flag: `-ef=x` is an unknown option
        if (!RequestsHelpOrVersion(parseResult) && !UnknownOptionGuard.AcceptsUnknownOptions(parseResult) && unknownShort is not null)
        {
            Console.Error.WriteLine(CommanderErrors.UnknownOption(unknownShort, CommanderErrors.PathOf(parseResult)));
            return 1;
        }

        if (!RequestsHelpOrVersion(parseResult) && CommanderErrors.MissingArgument(parseResult) is { } missing)
        {
            Console.Error.WriteLine(missing);
            return 1;
        }

        // commander: allowExcessArguments(false) commands reject extra operands (after the required-option check)
        if (!RequestsHelpOrVersion(parseResult) && CommanderErrors.ExcessArguments(parseResult) is { } excess)
        {
            Console.Error.WriteLine(excess);
            return 1;
        }

        // root options without a command (`dotenvx -q`) behave like no arguments: commander prints the help and exits 1
        if (ReferenceEquals(parseResult.CommandResult.Command, root) && !RequestsHelpOrVersion(parseResult) &&
            !parseResult.UnmatchedTokens.Any())
        {
            root.Parse(["--help"]).Invoke();
            return 1;
        }

        if (!RequestsHelpOrVersion(parseResult))
        {
            AnnounceLogLevel(host);
        }

        return parseResult.Invoke();
    }

    /// <summary>dotenvx.js preAction hook: `logger.debug(`setting log level to: ...`)` runs only once a command is about to execute.</summary>
    private static void AnnounceLogLevel(IHost host)
    {
        var options = host.Services.GetRequiredService<DotenvxLoggerOptions>();
        host.Services.GetRequiredService<IDotenvxLogger>().Debug($"setting log level to: {options.LogLevel.ToString().ToLowerInvariant()}");
    }

    private static readonly System.Text.RegularExpressions.Regex ColonLongOption = new("^--[A-Za-z][A-Za-z0-9-]*:");

    private static bool RequestsHelpOrVersion(ParseResult parseResult)
    {
        return parseResult.Tokens
            .TakeWhile(t => t.Value != "--")
            .Any(t => t.Type == System.CommandLine.Parsing.TokenType.Option && t.Value is "-h" or "--help" or "-V" or "--version");
    }
}
