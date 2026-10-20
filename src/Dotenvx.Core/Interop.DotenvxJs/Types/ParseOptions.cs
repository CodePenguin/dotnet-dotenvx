using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx;

public class ParseOptions
{
    public IDotenvxLogger? Logger { get; set; }
    public string? PrivateKey { get; set; }
    public IReadOnlyDictionary<string, string>? ProcessEnv { get; set; }
    public bool? Overload { get; set; } = false;
    public string[]? Ignore { get; set; }
}