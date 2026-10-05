using System;
using System.Linq;
using System.CommandLine;

using Dotenvx.Helpers;

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

        // dotenvx.js: `program.on('command:*')` prints this line to stdout and exits 0
        if (ReferenceEquals(parseResult.CommandResult.Command, root) &&
            parseResult.Tokens.FirstOrDefault(t => t.Type == System.CommandLine.Parsing.TokenType.Argument) is { } unknown)
        {
            Console.Out.WriteLine($"error: unknown command '{unknown.Value}'");
            return 0;
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
