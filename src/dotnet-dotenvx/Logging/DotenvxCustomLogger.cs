using System;

using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx.Logging;

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

        if (logLevel is LogLevel.Error or LogLevel.Critical)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}