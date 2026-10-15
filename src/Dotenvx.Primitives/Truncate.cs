namespace Dotenvx.Primitives;

/// <remarks>Reference source: dotenvx/src/lib/helpers/truncate.js (always appends the ellipsis, even to a short value)</remarks>
public static partial class Primitives
{
    public static string Truncate(string? value, int showChar = 7)
    {
        return value is { Length: > 0 } ? value[..Math.Min(showChar, value.Length)] + "…" : string.Empty;
    }
}
