namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class ParseOptions
{
    public string? PrivateKey { get; set; } = null;
    public IDictionary<string, string>? ProcessEnv { get; set; } = null;
    public bool Overload { get; set; } = false;
}