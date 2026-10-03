namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `remove` export - a line-by-line port of the real function
/// body (read via `Function.prototype.toString()`), checked against the live package by
/// PrimitivesOracleParityTest. Line based, not regex based: a lone `\r` is a line break, the file's
/// `\r\n` style is restored on output, and an unterminated quote swallows the rest of the file.
/// </remarks>
public static partial class Primitives
{
    public static string Remove(string? src, string key)
    {
        src ??= "";
        string newline = src.Contains("\r\n") ? "\r\n" : "\n";
        string normalized = src.Replace("\r\n", "\n").Replace("\r", "\n");
        bool endsWithNewline = normalized.EndsWith('\n');
        List<string> lines = [.. normalized.Split('\n')];
        if (endsWithNewline)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        List<string> kept = [];
        int i = 0;
        bool removed = false;
        while (i < lines.Count)
        {
            string line = lines[i];
            (string Name, string Raw)? parsed = ParseAssignment(line);
            if (parsed is not { } p || p.Name != key)
            {
                kept.Add(line);
                i++;
                continue;
            }

            removed = true;
            string raw = p.Raw;
            string trimmed = JsTrimStart(raw);
            char quote = trimmed.Length > 0 && trimmed[0] is '\'' or '"' or '`' ? trimmed[0] : '\0';
            if (quote != '\0' && !HasClosingQuote(trimmed, quote))
            {
                i++;
                while (i < lines.Count)
                {
                    raw += "\n" + lines[i];
                    if (HasClosingQuote(JsTrimStart(raw), quote))
                    {
                        break;
                    }

                    i++;
                }
            }

            i++;
        }

        if (!removed)
        {
            return src;
        }

        string result = string.Join("\n", kept);
        if (endsWithNewline && result.Length > 0)
        {
            result += "\n";
        }

        return newline == "\r\n" ? result.Replace("\n", "\r\n") : result;
    }

    private static (string Name, string Raw)? ParseAssignment(string line)
    {
        string t = JsTrimStart(line);
        if (t.StartsWith("export ", StringComparison.Ordinal))
        {
            t = JsTrimStart(t[7..]);
        }

        int eq = t.IndexOf('=');
        int colon = t.IndexOf(": ", StringComparison.Ordinal);
        if (eq == -1 || (colon != -1 && colon < eq))
        {
            eq = colon;
        }

        if (eq == -1)
        {
            return null;
        }

        string name = JsTrim(t[..eq]);
        if (name.Length == 0 || !name.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '.' or '-'))
        {
            return null;
        }

        int skip = t.AsSpan(eq).StartsWith(": ") ? 2 : 1;
        return (name, t[(eq + skip)..]);
    }

    // At least two unescaped occurrences of the quote character.
    private static bool HasClosingQuote(string s, char quote)
    {
        bool escaped = false;
        int count = 0;
        foreach (char c in s)
        {
            if (c == quote && !escaped)
            {
                count++;
            }

            escaped = c == (char)92 && !escaped;
        }

        return count >= 2;
    }
}
