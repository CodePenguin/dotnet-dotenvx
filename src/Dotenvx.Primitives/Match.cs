using Dotenvx.Interop.PicomatchJs;

namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `match(globs, options)` export (picomatch): returns a predicate.
/// </remarks>
public static partial class Primitives
{
    public static Func<string, bool> Match(string[]? globs = null, string[]? ignore = null, bool dot = false)
    {
        return IndexJs.Picomatch(globs, ignore, dot);
    }
}
