namespace Dotenvx;

public enum DotenvxLogLevel
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Verbose = 4,
    Debug = 5,
    Silly = 6
}

internal static class DotenvxLogLevelExtensions
{
    public static DotenvxLogLevel FromString(string value)
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
