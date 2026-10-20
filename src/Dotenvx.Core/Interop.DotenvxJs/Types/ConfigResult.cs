namespace Dotenvx;

public class ConfigResult
{
    public required IReadOnlyDictionary<string, string> Parsed { get; init; }
    public Exception? Error { get; init; }
}