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

    // `options.mask` upstream is `true` (default 6 visible chars) or a specific number - kept as
    // object? (not bool?/int?) to match Helpers.Mask's own dynamic showChar parameter shape.
    public object? Mask { get; set; }

    // JS's `{ ...options, ignore: [...] }`-style non-mutating spread (see
    // NormalizeDotenvConfigIgnore.cs) needs a way to copy an options object without losing a
    // derived type's own fields (GetOptions' All/Format, etc). MemberwiseClone operates on the
    // actual runtime object regardless of the static reference type it's called through, so this
    // correctly clones a GetOptions passed in as a plain ConfigOptions reference.
    public ConfigOptions Clone()
    {
        return (ConfigOptions)MemberwiseClone();
    }
}