namespace Dotenvx.Primitives;

/// <remarks>Reference source: dotenvx/src/lib/helpers/truncate.js</remarks>
public static partial class Primitives
{
    public static string Truncate(string? value, int totalLength = 8)
    {
        return value is { Length: > 0 }
            ? value.Length > totalLength ? value[..(totalLength - 1)] + "…" : value
            : string.Empty;
    }
}
