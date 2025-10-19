namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    private static string? SearchProcessEnv(string privateKeyName)
    {
        string? value = Environment.GetEnvironmentVariable(privateKeyName);
        return !string.IsNullOrEmpty(value) ? value : null;
    }
}