using System.CommandLine;

using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx.Extensions;

public static class LoggingBuilderExtensions
{
    public static void SetDotenvxLogLevel(this ILoggingBuilder builder, string[] args)
    {
        LoggingRootCommand rootCommand = new();
        rootCommand.SetAction(parseResult =>
        {
            rootCommand.SetLogLevel(args, logLevel => builder.AddFilter(nameof(dotnet_dotenvx), logLevel));
            rootCommand.SetLogLevel(args, logLevel => builder.AddFilter(nameof(Dotenvx.Core), logLevel));
        });
        ParseResult parseResult = rootCommand.Parse(args);
        parseResult.Invoke();
    }
}