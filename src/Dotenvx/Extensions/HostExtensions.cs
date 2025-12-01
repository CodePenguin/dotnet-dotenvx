using System.CommandLine;

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
        return parseResult.Invoke();
    }
}