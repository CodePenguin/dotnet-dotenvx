namespace Dotenvx.Core.Interop.Dotenvx.Types;

public class ParseOptions
{
    public string? PrivateKey { get; set; } = null;
    public IDictionary<string, string>? ProcessEnv { get; set; } = null;
    public bool? Overload { get; set; } = false;
    public bool? Override { get; set; } = false;
}