namespace Dotenvx.Core.Interop.Dotenvx.Types;

public class ConfigOptions
{
    public List<string>? Path { get; set; }
    public bool Overload { get; set; }
    public IDictionary<string, string>? ProcessEnv { get; set; }
    public List<string>? Ignore { get; set; }
    public bool Strict { get; set; }
    public string? EnvKeysFile { get; set; }
    public string? Convention { get; set; }
}