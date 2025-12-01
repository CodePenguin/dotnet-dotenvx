namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/searchProcessEnv.js</remarks>
public static partial class Helpers
{
    private static string? SearchProcessEnv(string privateKeyName)
    {
        string? value = Environment.GetEnvironmentVariable(privateKeyName);
        return !string.IsNullOrEmpty(value) ? value : null;
    }
}