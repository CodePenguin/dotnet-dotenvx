using System;
using System.Collections.Generic;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Parsing;

using Dotenvx.Help;
using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

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

        ParseResult parseResult = root.Parse(CommanderArgs.Normalize(args, out string? missingTerm));

        // dotenvx.js: the root's `[command] [args...]` action runs executeDynamic: `dotenvx-<command>` from PATH, else
        // `error: unknown command '<command>'` on stdout and exit 0
        // (commander handles -h/--help before the action runs, so `dotenvx foo --help` prints the root help and exits 0)
        if (ReferenceEquals(parseResult.CommandResult.Command, root) && !RequestsHelpOrVersion(parseResult) &&
            parseResult.UnmatchedTokens.FirstOrDefault() is { } unknownCommand)
        {
            // `process.argv.slice(3)`: everything after the first user argument
            return ExecuteDynamic.Dynamic(unknownCommand, args[1..], host.Services.GetRequiredService<IDotenvxLogger>());
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

        // commander: `error: option '-f, --env-file <path>' argument missing` (stderr, exit 1)
        if (!RequestsHelpOrVersion(parseResult) && missingTerm is not null)
        {
            Console.Error.WriteLine($"error: option '{missingTerm}' argument missing");
            return 1;
        }

        if (!RequestsHelpOrVersion(parseResult) && CommanderErrors.MissingArgument(parseResult) is { } missing)
        {
            Console.Error.WriteLine(missing);
            return 1;
        }

        return parseResult.Invoke();
    }

    private static readonly System.Text.RegularExpressions.Regex ColonLongOption = new("^--[A-Za-z][A-Za-z0-9-]*:");

    private static bool RequestsHelpOrVersion(ParseResult parseResult)
    {
        return parseResult.Tokens
            .TakeWhile(t => t.Value != "--")
            .Any(t => t.Type == System.CommandLine.Parsing.TokenType.Option && t.Value is "-h" or "--help" or "-V" or "--version");
    }
}
