using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx;

public class ParseOptions
{
    public IDotenvxLogger? Logger { get; set; }
    public string? PrivateKey { get; set; }
    public IReadOnlyDictionary<string, string>? ProcessEnv { get; set; }
    public bool? Overload { get; set; } = false;

    /// <summary>`options.override`: an alias of <see cref="Overload" /> (main.js `options.overload || options.override`).</summary>
    public bool? Override { get; set; }
    public string[]? Ignore { get; set; }
}
