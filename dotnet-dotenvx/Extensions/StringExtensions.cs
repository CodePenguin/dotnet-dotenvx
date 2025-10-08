namespace dotnet_dotenvx.Extensions;

public static class StringExtensions
{
    public static string Truncate(this string? value, int showCharacters = 7)
    {
        return value is { Length: > 0 }
            ? value[..showCharacters] + "…"
            : string.Empty;
    }
}