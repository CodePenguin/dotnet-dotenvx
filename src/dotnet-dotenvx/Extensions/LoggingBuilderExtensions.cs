using System.CommandLine;
using System.IO;

using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx.Extensions;

public static class LoggingBuilderExtensions
{
    public static void SetDotenvxLogLevel(this ILoggingBuilder builder, string[] args)
    {
        LoggingRootCommand rootCommand = new() { TreatUnmatchedTokensAsErrors = false };
        rootCommand.SetAction(parseResult =>
        {
            rootCommand.SetLogLevel(parseResult, logLevel =>
            {
                builder.AddFilter(nameof(dotnet_dotenvx), logLevel);
                builder.AddFilter(nameof(Dotenvx.Core), logLevel);
            });
        });

        ParseResult parseResult = rootCommand.Parse(args);
        parseResult.Invoke(new InvocationConfiguration { Error = TextWriter.Null, Output = TextWriter.Null });
    }
}