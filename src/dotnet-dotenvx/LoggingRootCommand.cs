using System.CommandLine;
using System.Linq;

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

    private string? LogLevelFlagsToLogLevel(ParseResult parseResult)
    {
        (Option<bool>, string) mappings = new[]
            {
                (_debugLogLevel, "debug"), (_verboseLogLevel, "verbose"), (_quietLogLevel, "error")
            }
            .FirstOrDefault(mapping => parseResult.GetValue(mapping.Item1));
        return mappings.Item1 != null ? mappings.Item2 : null;
    }

    public string GetLogLevel(ParseResult parseResult)
    {
        return
            parseResult.GetValue(_logLevel)
            ?? LogLevelFlagsToLogLevel(parseResult)
            ?? "info";
    }

    public bool IsVerboseMode(ParseResult parseResult)
    {
        return parseResult.GetValue(_logLevel) == "verbose" || parseResult.GetValue(_verboseLogLevel);
    }
}