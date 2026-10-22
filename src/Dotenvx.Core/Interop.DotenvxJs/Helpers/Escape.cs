namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/escape.js</remarks>
internal static partial class Helpers
{
    public static string Escape(string value)
    {
        const char quote = '\'';
        return quote + value.Replace(quote.ToString(), $"{quote}\\{quote}{quote}") + quote;
    }
}
