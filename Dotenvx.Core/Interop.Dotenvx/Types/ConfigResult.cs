namespace Dotenvx.Core.Interop.Dotenvx.Types;

public class ConfigResult
{
    public required IDictionary<string, string> Parsed { get; set; }
    public required string Error { get; set; }
}