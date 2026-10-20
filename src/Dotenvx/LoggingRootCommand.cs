using System.CommandLine;
using System.Linq;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx;

public class LoggingRootCommand : RootCommand
{
    // Log Level Options
    // Recursive = true so these are also recognized when placed after a subcommand (e.g.
    // `dotenvx get -v`), matching commander.js's global-option behavior. Without it,
    // System.CommandLine only recognizes them before the subcommand name, and a subcommand
    // with an optional positional argument (e.g. `get [KEY]`, `ls [directory]`) silently
    // swallows the flag token as that argument's value instead of erroring or routing it here.
    private readonly Option<bool> _debugLogLevel =
        new("--debug", "-d") { Description = "sets log level to debug", Recursive = true };

    private readonly Option<string> _logLevel =
        new("--log-level", "-l")
        {
            Description = "set log level (default: \"info\")", HelpName = "level", Recursive = true
        };

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
            ?? EnvQuietToLogLevel()
            ?? "info";
    }

    private static string? EnvQuietToLogLevel()
    {
        return NormalizeDotenvConfigQuiet(new ConfigOptions()).Quiet == true
            ? "error"
            : null;
    }

    public bool IsQuiet(ParseResult parseResult)
    {
        return parseResult.GetValue(_quietLogLevel);
    }

    /// <summary>`options.verbose` (the -v flag only).</summary>
    public bool IsVerboseFlag(ParseResult parseResult)
    {
        return parseResult.GetValue(_verboseLogLevel);
    }

    /// <summary>`options.debug` (the -d flag only).</summary>
    public bool IsDebugFlag(ParseResult parseResult)
    {
        return parseResult.GetValue(_debugLogLevel);
    }

    public bool IsVerboseMode(ParseResult parseResult)
    {
        return parseResult.GetValue(_logLevel) == "verbose" || parseResult.GetValue(_verboseLogLevel);
    }
}