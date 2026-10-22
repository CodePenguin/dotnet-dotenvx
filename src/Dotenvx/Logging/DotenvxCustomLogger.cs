using System;

using Microsoft.Extensions.Logging;

namespace Dotenvx.Logging;

public class DotenvxCustomLogger(DotenvxCustomLoggerConfiguration configuration) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= configuration.MinimumLogLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        if (exception != null)
        {
            message = $"{message}\n{exception}";
        }

        // a running spinner is not stopped here: yocto-spinner wipes itself before any terminal output and redraws after it
        // (see YoctoSpinner's stream hook); actions stop it explicitly where upstream does
        // Only "info" goes to stdout upstream (dotenvx/src/shared/logger.js) - everything else
        // (error, infoerror, warn, success, help, verbose, debug) goes to stderr, keeping stdout
        // reserved for actual data (e.g. `get`'s returned values) so it stays script-safe.
        if (eventId.Id == DotenvxLogEvents.Info.Id)
        {
            Console.WriteLine(message);
        }
        else
        {
            Console.Error.WriteLine(message);
        }
    }
}
