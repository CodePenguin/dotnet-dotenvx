namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/conventions/filepaths.js - always an array of env file paths, defaulting to `.env`.</remarks>
internal static partial class Helpers
{
    public static string[] Filepaths(string? envFile = null)
    {
        return [envFile ?? ".env"];
    }

    public static string[] Filepaths(string[]? envFile)
    {
        return envFile ?? [".env"];
    }
}
