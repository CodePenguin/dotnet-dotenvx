using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dotenvx.Extensions;

public static class LoggingBuilderExtensions
{
    public static void AddDotenvx(this ILoggingBuilder builder, DotenvxLoggerOptions options)
    {
        LogLevel minimumLogLevel = DotenvxLogLevelToLogLevel(options.LogLevel);
        builder.AddFilter<DotenvxLoggerProvider>(null, minimumLogLevel);
        builder.Services.AddSingleton<ILoggerProvider>(_ =>
            new DotenvxLoggerProvider(new DotenvxCustomLoggerConfiguration { MinimumLogLevel = minimumLogLevel }));
    }

    private static LogLevel DotenvxLogLevelToLogLevel(DotenvxLogLevel logLevel)
    {
        return logLevel switch
        {
            DotenvxLogLevel.Debug or DotenvxLogLevel.Verbose => LogLevel.Debug,
            DotenvxLogLevel.Error => LogLevel.Error,
            DotenvxLogLevel.Silly => LogLevel.Trace,
            DotenvxLogLevel.Warn => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }
}