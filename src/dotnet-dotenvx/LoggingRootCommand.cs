using System;
using System.CommandLine;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx;

public class LoggingRootCommand : RootCommand
{
    // Log Level Options
    private readonly Option<bool> _debugLogLevel =
        new("--debug", "-d") { Description = "sets log level to debug", Recursive = true };

    private readonly Option<string> _logLevel =
        new("--log-level", "-l") { Description = "set log level", Recursive = true };

    private readonly Option<bool> _quietLogLevel =
        new("--quiet", "-q") { Description = "sets log level to error", Recursive = true };

    private readonly Option<bool> _verboseLogLevel =
        new("--verbose", "-v") { Description = "sets log level to verbose", Recursive = true };

    public LoggingRootCommand(string description = "") : base(description)
    {
        // Add Options
        Add(_logLevel);
        Add(_quietLogLevel);
        Add(_verboseLogLevel);
        Add(_debugLogLevel);
    }

    private LogLevel? LogLevelFlagsToLogLevel(ParseResult parseResult)
    {
        (Option<bool>, LogLevel) mappings = new[]
            {
                (_quietLogLevel, LogLevel.Error), (_debugLogLevel, LogLevel.Debug),
                (_verboseLogLevel, LogLevel.Trace)
            }
            .FirstOrDefault(mapping => parseResult.GetValue(mapping.Item1));
        return mappings.Item1 != null ? mappings.Item2 : null;
    }

    public void SetLogLevel(ParseResult parseResult, Action<LogLevel> callback)
    {
        LogLevel logLevel =
            ToLogLevel(parseResult.GetValue(_logLevel))
            ?? LogLevelFlagsToLogLevel(parseResult)
            ?? LogLevel.Information;
        callback(logLevel);
    }

    private static LogLevel? ToLogLevel(string? getValue)
    {
        return getValue?.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "error" => LogLevel.Error,
            "info" => LogLevel.Information,
            "silly" or "verbose" => LogLevel.Trace,
            "warn" => LogLevel.Warning,
            _ => null
        };
    }
}