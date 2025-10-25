namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class ConfigResult
{
    public required IDictionary<string, string> Parsed { get; init; }
    public Exception? Error { get; init; }
}