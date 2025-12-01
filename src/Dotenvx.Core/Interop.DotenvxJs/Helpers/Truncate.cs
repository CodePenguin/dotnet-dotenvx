namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/truncate.js</remarks>
public static partial class Helpers
{
    public static string Truncate(string? value, int showCharacters = 7)
    {
        return value is { Length: > 0 }
            ? value.Length > showCharacters ? value[..showCharacters] + "…" : value
            : string.Empty;
    }
}