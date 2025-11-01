using System.CommandLine;
using System.IO;

using Dotenvx.Core.Interop.DotenvxJs.Shared;

using dotnet_dotenvx.Actions;

using Microsoft.Extensions.DependencyInjection;

namespace dotnet_dotenvx.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddDotenvxCommands(this IServiceCollection services)
    {
        services.AddTransient<DotenvxRootCommand>();
        services.AddTransient<RunAction>();
    }

    public static void AddDotenvxLogging(this IServiceCollection services, string[] args)
    {
        DotenvxLoggerConfiguration configuration = GetDotenvxLoggerConfiguration(args);
        services.AddSingleton(configuration);
        services.AddSingleton<IDotenvxLogger, DotenvxLogger>();
    }

    private static DotenvxLoggerConfiguration GetDotenvxLoggerConfiguration(string[] args)
    {
        string logLevel = "info";
        bool verbose = false;

        LoggingRootCommand rootCommand = new() { TreatUnmatchedTokensAsErrors = false };
        rootCommand.SetAction(parseResult =>
        {
            logLevel = rootCommand.GetLogLevel(parseResult);
            verbose = rootCommand.IsVerboseMode(parseResult);
        });

        ParseResult parseResult = rootCommand.Parse(args);
        parseResult.Invoke(new InvocationConfiguration { Error = TextWriter.Null, Output = TextWriter.Null });

        return new DotenvxLoggerConfiguration { LogLevel = logLevel, Verbose = verbose };
    }
}