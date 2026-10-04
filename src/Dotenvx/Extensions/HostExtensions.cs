using System;
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
        string[] arguments = args.Length > 0 ? args : ["--help"];
        ParseResult parseResult = root.Parse(arguments);

        // commander rejects unknown options even though it tolerates extra positional values (see UnknownOptionGuard)
        if (UnknownOptionGuard.FindUnknownOption(parseResult) is { } unknownOption)
        {
            Console.Error.WriteLine($"error: unknown option '{unknownOption}'");
            return 1;
        }

        return parseResult.Invoke();
    }
}
