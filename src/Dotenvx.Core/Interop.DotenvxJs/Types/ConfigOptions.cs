using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Types;

public class ConfigOptions
{
    public IDotenvxLogger? Logger { get; set; }
    public List<string>? Path { get; set; }

    // JS `options.path` given as a single string: split on commas, trimmed, blanks dropped (an array in `Path` is used as-is)
    public string? PathString { get; set; }
    public bool? Overload { get; set; }
    public IDictionary<string, string>? ProcessEnv { get; set; }
    public List<string>? Ignore { get; set; }
    public bool? Strict { get; set; }
    public string? EnvKeysFile { get; set; }
    public bool? NoNative { get; set; }
    public bool? No1Password { get; set; }
    public bool? NoBitwarden { get; set; }

    /// <summary>`options.noArmor`: skip Dotenvx Armor key access.</summary>
    public bool? NoArmor { get; set; }

    /// <summary>`options.token`: the Armor token (forces Armor on, even when the session is off).</summary>
    public string? Token { get; set; }

    /// <summary>`options.profile`: the Envfile profile that selects the env files.</summary>
    public string? Profile { get; set; }

    // main.js `options.envs`: explicit env items (files or raw `KEY=value` strings) used as-is, ignoring `Path` and `Convention`
    public EnvItem[]? Envs { get; set; }

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