using System.Globalization;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/mask.js</remarks>
public static partial class Helpers
{
    public static string Mask(string? str, object? showChar = null)
    {
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }

        double number = showChar switch
        {
            null => 6,
            string value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : double.NaN,
            _ => Convert.ToDouble(showChar, CultureInfo.InvariantCulture)
        };

        double showCharCount = 0;
        if (double.IsFinite(number))
        {
            showCharCount = Math.Abs(Math.Truncate(number));
        }

        double visibleChars = Math.Min(showCharCount, Math.Ceiling(str.Length / 2d));
        if (str.Length == 1)
        {
            visibleChars = 0;
        }

        int visibleCount = (int)visibleChars;
        string visiblePart = str[..visibleCount];
        string maskedPart = new('*', str.Length - visibleCount);

        return visiblePart + maskedPart;
    }
}
