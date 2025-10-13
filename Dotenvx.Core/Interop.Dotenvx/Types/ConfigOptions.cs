namespace Dotenvx.Core.Interop.Dotenvx.Types;

public class ConfigOptions
{
    public List<string>? Path { get; init; }
    public bool Overload { get; init; } = false;
    public IDictionary<string, string>? ProcessEnv { get; init; }
    public List<string>? Ignore { get; init; }
    public bool Strict { get; init; } = false;
    public string? EnvKeysFile { get; init; }
    public string? Convention { get; init; }
}