namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/redact.js</remarks>
internal static partial class Helpers
{
    public static string Redact(string? str)
    {
        return string.IsNullOrEmpty(str) ? string.Empty : "[REDACTED]";
    }
}
