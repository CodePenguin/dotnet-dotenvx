using System.Text;

namespace Dotenvx.Interop.EnquirerJs;

/// <remarks>Reference source: enquirer lib/ansi.js (cursor movement, erasing and the cursor-restore arithmetic).</remarks>
internal static class Ansi
{
    public const string Esc = "\u001b[";

    public const string Bell = "\u0007";

    public static readonly bool IsAppleTerminal = Environment.GetEnvironmentVariable("TERM_PROGRAM") == "Apple_Terminal";

    public static string Hide => Esc + "?25l";

    public static string Show => Esc + "?25h";

    public static string Beginning => Esc + "G";

    public static string Line => Esc + "2K";

    public static string Up(int count = 1) => count != 0 ? $"{Esc}{count}A" : string.Empty;

    public static string Down(int count = 1) => count != 0 ? $"{Esc}{count}B" : string.Empty;

    public static string Right(int count = 1) => count != 0 ? $"{Esc}{count}C" : string.Empty;

    public static string Left(int count = 1) => count != 0 ? $"{Esc}{count}D" : string.Empty;

    public static string PrevLine(int count = 1)
    {
        return string.Concat(Enumerable.Repeat(Esc + "F", count));
    }

    /// <summary>`cursor.to(x, y)`: column x (1-based on the wire) or the absolute position when y is given.</summary>
    public static string To(int x, int y = 0)
    {
        return y != 0 ? $"{Esc}{y + 1};{x + 1}H" : $"{Esc}{x + 1}G";
    }

    public static string Move(int x = 0, int y = 0)
    {
        string result = x < 0 ? Left(-x) : x > 0 ? Right(x) : string.Empty;

        return result + (y < 0 ? Up(-y) : y > 0 ? Down(y) : string.Empty);
    }

    /// <summary>`cursor.strLen`: ASCII counts 1, everything else 2.</summary>
    public static int StrLen(string text)
    {
        int length = 0;
        foreach (char character in text)
        {
            length += character <= 128 ? 1 : 2;
        }

        return length;
    }

    /// <summary>`cursor.restore(state)`: returns null when there is nothing to restore (JS returns undefined).</summary>
    public static string? Restore(string after, int cursor, string initial, string input, string prompt, int size, string value)
    {
        if (size != 0)
        {
            string codes = Up(size) + To(StrLen(prompt));
            int diff = input.Length - cursor;
            if (diff > 0)
            {
                codes += Left(diff);
            }

            return codes;
        }

        if (value.Length > 0 || after.Length > 0)
        {
            int pos = input.Length == 0 && initial.Length > 0 ? -StrLen(initial) : -StrLen(input) + cursor;
            if (after.Length > 0)
            {
                pos -= StrLen(after);
            }

            if (input.Length == 0 && initial.Length > 0 && !prompt.Contains(initial, StringComparison.Ordinal))
            {
                pos += StrLen(initial);
            }

            return Move(pos);
        }

        return null;
    }

    /// <summary>`ansi.clear(input, columns)`: erase the lines the printed text occupied and return to the first column.</summary>
    public static string Clear(string input, int? columns)
    {
        if (columns is null or 0)
        {
            return Line + To(0);
        }

        int rows = 0;
        foreach (string line in input.Split('\n').Select(item => item.TrimEnd('\r')))
        {
            int width = Style.StripAnsi(line).EnumerateRunes().Count();
            rows += 1 + (int)Math.Floor(Math.Max(width - 1, 0) / (double)columns.Value);
        }

        var result = new StringBuilder();
        for (int i = 0; i < rows - 1; i++)
        {
            result.Append(Line).Append(PrevLine());
        }

        return result.Append(Line).Append(To(0)).ToString();
    }
}
