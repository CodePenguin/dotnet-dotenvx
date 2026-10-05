using System;
using System.Linq;
using System.CommandLine;

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

        ParseResult parseResult = root.Parse(args);

        // dotenvx.js: the root's `[command] [args...]` action runs executeDynamic: `dotenvx-<command>` from PATH, else
        // `error: unknown command '<command>'` on stdout and exit 0
        if (ReferenceEquals(parseResult.CommandResult.Command, root) &&
            parseResult.Tokens.FirstOrDefault(t => t.Type == System.CommandLine.Parsing.TokenType.Argument) is { } unknown)
        {
            // `process.argv.slice(3)`: everything after the first user argument
            return ExecuteDynamic.Dynamic(unknown.Value, args[1..], host.Services.GetRequiredService<IDotenvxLogger>());
        }

        // commander rejects unknown options even though it tolerates extra positional values (see UnknownOptionGuard)
        if (UnknownOptionGuard.FindUnknownOption(parseResult) is { } unknownOption)
        {
            Console.Error.WriteLine($"error: unknown option '{unknownOption}'");
            return 1;
        }

        return parseResult.Invoke();
    }
}
