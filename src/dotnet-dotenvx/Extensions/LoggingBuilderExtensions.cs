using Dotenvx.Core.Interop.DotenvxJs.Shared;

using dotnet_dotenvx.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx.Extensions;

public static class LoggingBuilderExtensions
{
    public static void AddDotenvx(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider>(serviceProvider =>
        {
            DotenvxLoggerConfiguration configuration = serviceProvider.GetRequiredService<DotenvxLoggerConfiguration>();

            LogLevel minimumLogLevel = DotenvxLogLevelToLogLevel(configuration.LogLevel);

            //builder.SetMinimumLevel(minimumLogLevel);
            builder.AddFilter<DotenvxLoggerProvider>(null, minimumLogLevel);
            builder.AddFilter(nameof(dotnet_dotenvx), minimumLogLevel);
            builder.AddFilter(nameof(Dotenvx.Core), minimumLogLevel);

            return new DotenvxLoggerProvider(new DotenvxCustomLoggerConfiguration
            {
                MinimumLogLevel = minimumLogLevel
            });
        });
    }

    private static LogLevel DotenvxLogLevelToLogLevel(string getValue)
    {
        return getValue.ToLowerInvariant() switch
        {
            "debug" or "verbose" => LogLevel.Debug,
            "error" => LogLevel.Error,
            "silly" => LogLevel.Trace,
            "warn" => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }
}