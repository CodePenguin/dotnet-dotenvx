using System.CommandLine;
using System.IO;

using dotnet_dotenvx.Actions;
using dotnet_dotenvx.Logging;

namespace dotnet_dotenvx;

public class DotenvxRootCommand : LoggingRootCommand
{
    public DotenvxRootCommand(
        RunAction runAction
    ) : base("A secure dotenv for .NET – based on dotenvx from the creator of `dotenv`")
    {
        // Add Actions
        Subcommands.Add(runAction);
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

        return new DotenvxLoggerOptions { LogLevel = DotenvxLogLevelToLogLevel(logLevel) };
    }

    private static DotenvxLogLevel DotenvxLogLevelToLogLevel(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "debug" => DotenvxLogLevel.Debug,
            "error" => DotenvxLogLevel.Error,
            "silly" => DotenvxLogLevel.Silly,
            "verbose" => DotenvxLogLevel.Verbose,
            "warn" => DotenvxLogLevel.Warn,
            _ => DotenvxLogLevel.Info
        };
    }
}