using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Types;

public class ConfigOptions
{
    public IDotenvxLogger? Logger { get; set; }
    public List<string>? Path { get; set; }
    public bool? Overload { get; set; }
    public IDictionary<string, string>? ProcessEnv { get; set; }
    public List<string>? Ignore { get; set; }
    public bool? Strict { get; set; }
    public string? EnvKeysFile { get; set; }

    public string? Convention { get; set; }
    public bool? Debug { get; set; }
    public bool? Quiet { get; set; }
    public bool? Verbose { get; set; }
    public string? LogLevel { get; set; }
}