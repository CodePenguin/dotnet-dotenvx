using System.Text.RegularExpressions;

namespace Dotenvx.Interop.EnquirerJs;

/// <remarks>
/// Reference source: ansi-colors (lib/index.js `style`/`wrap`, bundled by enquirer). A style is a stack of open/close code pairs;
/// the last style of the stack wraps the text first, so <c>Cyan.Underline</c> renders as ESC[36m ESC[4m text ESC[24m ESC[39m.
/// Colours are on unless FORCE_COLOR=0 (ansi-colors does not look at the terminal or NO_COLOR).
/// </remarks>
internal sealed class Style
{
    private static readonly Regex AnsiRegex = new(
        "[\u001b\u009b][\\[\\]#;?()]*(?:(?:(?:[^\\W_]*;?[^\\W_]*)\u0007)|(?:(?:[0-9]{1,4}(;[0-9]{0,4})*)?[~0-9=<>cf-nqrtyA-PRZ]))",
        RegexOptions.Compiled);

    // strip-ansi / ansi-regex (the pattern enquirer's own width calculations use)
    private static readonly Regex StripRegex = new(
        "[\u001B\u009B][\\[\\]()#;?]*(?:(?:(?:[a-zA-Z\\d]*(?:;[-a-zA-Z\\d\\/#&.:=?%@~_]*)*)?\u0007)|(?:(?:\\d{1,4}(?:;\\d{0,4})*)?[\\dA-PR-TZcf-ntqry=><~]))",
        RegexOptions.Compiled);

    private readonly (string Open, string Close)[] _stack;

    private Style(params (string Open, string Close)[] stack)
    {
        _stack = stack;
    }

    /// <summary>ansi-colors' `enabled`: false only when FORCE_COLOR is "0".</summary>
    public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("FORCE_COLOR") != "0";

    public static Style Noop { get; } = new();

    public static Style Cyan { get; } = Pair(36, 39);

    public static Style Green { get; } = Pair(32, 39);

    public static Style Magenta { get; } = Pair(35, 39);

    public static Style Yellow { get; } = Pair(33, 39);

    public static Style Black { get; } = Pair(30, 39);

    public static Style Gray { get; } = Pair(90, 39);

    public static Style Bold { get; } = Pair(1, 22);

    public static Style Dim { get; } = Pair(2, 22);

    public static Style BgCyan { get; } = Pair(46, 49);

    public Style Underline => Then(4, 24);

    public Style GrayStyle => Then(90, 39);

    public Style DimStyle => Then(2, 22);

    public Style BlackStyle => Then(30, 39);

    private static Style Pair(int open, int close)
    {
        return new Style(($"\u001b[{open}m", $"\u001b[{close}m"));
    }

    private Style Then(int open, int close)
    {
        return new Style([.. _stack, ($"\u001b[{open}m", $"\u001b[{close}m")]);
    }

    /// <summary>Style a text (ansi-colors `style`): empty and null input stay empty, newlines re-open the style after each break.</summary>
    public string Apply(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        if (!Enabled)
        {
            return input;
        }

        string text = input;
        bool newline = text.Contains('\n');
        for (int n = _stack.Length - 1; n >= 0; n--)
        {
            (string open, string close) = _stack[n];
            if (text.Contains(close, StringComparison.Ordinal))
            {
                text = text.Replace(close, close + open, StringComparison.Ordinal);
            }

            string output = open + text + close;
            text = newline ? Regex.Replace(output, "\r*\n", match => close + match.Value + open) : output;
        }

        return text;
    }

    /// <summary>enquirer's `inverse(primary)`: the background colour of the foreground, with black text.</summary>
    public static Style Inverse(Style primary)
    {
        return primary == Cyan ? new Style(BgCyan._stack[0], Black._stack[0]) : primary;
    }

    /// <summary>ansi-colors `hasColor`.</summary>
    public static bool HasColor(string? text)
    {
        return !string.IsNullOrEmpty(text) && AnsiRegex.IsMatch(text);
    }

    /// <summary>strip-ansi.</summary>
    public static string StripAnsi(string text)
    {
        return StripRegex.Replace(text, string.Empty);
    }

    /// <summary>ansi-colors `unstyle`.</summary>
    public static string Unstyle(string text)
    {
        return AnsiRegex.Replace(text, string.Empty);
    }
}

/// <remarks>
/// Reference source: enquirer lib/styles.js (the default palette) and lib/symbols.js (merged over ansi-colors' symbols, which
/// differ on Windows).
/// </remarks>
internal static class Palette
{
    public static Style Primary => Style.Cyan;

    public static Style Success => Style.Green;

    public static Style Danger => Style.Magenta;

    public static Style Strong => Style.Bold;

    public static Style Warning => Style.Yellow;

    public static Style Muted => Style.Dim;

    public static Style Disabled => Style.Gray;

    public static Style Dark => Style.Dim.GrayStyle;

    public static Style Em => Style.Cyan.Underline;

    public static Style Placeholder => Style.Cyan.DimStyle;

    public static Style ForStatus(string status)
    {
        return status switch
        {
            "submitted" => Success,
            "cancelled" => Danger,
            _ => Primary
        };
    }
}

/// <summary>The symbols one prompt uses, after the per-prompt overrides were merged over the defaults.</summary>
internal sealed class Symbols
{
    public static bool IsWindows { get; set; } = OperatingSystem.IsWindows();

    public static bool IsLinux { get; set; } = OperatingSystem.IsLinux();

    public string Pointer { get; set; } = IsWindows ? ">" : IsLinux ? "▸" : "❯";

    public string Ellipsis { get; set; } = IsWindows ? "..." : "…";

    public string Check { get; set; } = IsWindows ? "√" : "✔";

    public string Cross { get; set; } = IsWindows ? "×" : "✖";

    public string Question { get; set; } = "?";

    public string Middot { get; set; } = "·";

    public string PointerSmall { get; set; } = IsWindows ? "»" : IsLinux ? "‣" : "›";

    public string Line { get; set; } = "─";

    public string Asterisk { get; set; } = "*";

    public string Indicator => Check;

    public Dictionary<string, string> Prefix { get; } = new()
    {
        ["pending"] = IsWindows ? "?" : "?",
        ["submitted"] = IsWindows ? "√" : "✔",
        ["cancelled"] = IsWindows ? "×" : "✖"
    };

    public Dictionary<string, string> Separator { get; } = new()
    {
        ["pending"] = IsWindows ? "»" : IsLinux ? "‣" : "›",
        ["submitted"] = "·",
        ["cancelled"] = "·"
    };
}
