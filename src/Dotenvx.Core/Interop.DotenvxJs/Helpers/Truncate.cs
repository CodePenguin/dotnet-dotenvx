namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static string Truncate(string? value, int showCharacters = 7)
    {
        return value is { Length: > 0 }
            ? value[..showCharacters] + "…"
            : string.Empty;
    }
}