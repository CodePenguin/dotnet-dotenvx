using System.Text.RegularExpressions;

namespace Dotenvx.Interop.EnquirerJs;

/// <summary>A decoded key (the object enquirer's keypress handlers receive).</summary>
internal sealed class KeyEvent
{
    public string? Name { get; set; }

    public bool Ctrl { get; set; }

    public bool Meta { get; set; }

    public bool Shift { get; set; }

    public bool Option { get; set; }

    public string? Sequence { get; set; }

    public string? Raw { get; set; }

    public string? Code { get; set; }

    public string? Action { get; set; }

    public KeyEvent Clone()
    {
        return (KeyEvent)MemberwiseClone();
    }
}

/// <remarks>
/// Reference source: enquirer lib/keypress.js and lib/combos.js, plus Node's readline `emitKeys` (lib/internal/readline/utils.js),
/// which splits the input stream into the keys enquirer then re-parses from their raw sequence.
/// </remarks>
internal static partial class KeypressDecoder
{
    private static readonly Dictionary<string, string> KeyNames = new()
    {
        ["OP"] = "f1",
        ["OQ"] = "f2",
        ["OR"] = "f3",
        ["OS"] = "f4",
        ["[11~"] = "f1",
        ["[12~"] = "f2",
        ["[13~"] = "f3",
        ["[14~"] = "f4",
        ["[[A"] = "f1",
        ["[[B"] = "f2",
        ["[[C"] = "f3",
        ["[[D"] = "f4",
        ["[[E"] = "f5",
        ["[15~"] = "f5",
        ["[17~"] = "f6",
        ["[18~"] = "f7",
        ["[19~"] = "f8",
        ["[20~"] = "f9",
        ["[21~"] = "f10",
        ["[23~"] = "f11",
        ["[24~"] = "f12",
        ["[A"] = "up",
        ["[B"] = "down",
        ["[C"] = "right",
        ["[D"] = "left",
        ["[E"] = "clear",
        ["[F"] = "end",
        ["[H"] = "home",
        ["OA"] = "up",
        ["OB"] = "down",
        ["OC"] = "right",
        ["OD"] = "left",
        ["OE"] = "clear",
        ["OF"] = "end",
        ["OH"] = "home",
        ["[1~"] = "home",
        ["[2~"] = "insert",
        ["[3~"] = "delete",
        ["[4~"] = "end",
        ["[5~"] = "pageup",
        ["[6~"] = "pagedown",
        ["[[5~"] = "pageup",
        ["[[6~"] = "pagedown",
        ["[7~"] = "home",
        ["[8~"] = "end",
        ["[a"] = "up",
        ["[b"] = "down",
        ["[c"] = "right",
        ["[d"] = "left",
        ["[e"] = "clear",
        ["[2$"] = "insert",
        ["[3$"] = "delete",
        ["[5$"] = "pageup",
        ["[6$"] = "pagedown",
        ["[7$"] = "home",
        ["[8$"] = "end",
        ["Oa"] = "up",
        ["Ob"] = "down",
        ["Oc"] = "right",
        ["Od"] = "left",
        ["Oe"] = "clear",
        ["[2^"] = "insert",
        ["[3^"] = "delete",
        ["[5^"] = "pageup",
        ["[6^"] = "pagedown",
        ["[7^"] = "home",
        ["[8^"] = "end",
        ["[Z"] = "tab"
    };

    private static readonly string[] ShiftCodes = ["[a", "[b", "[c", "[d", "[e", "[2$", "[3$", "[5$", "[6$", "[7$", "[8$", "[Z"];

    private static readonly string[] CtrlCodes = ["Oa", "Ob", "Oc", "Od", "Oe", "[2^", "[3^", "[5^", "[6^", "[7^", "[8^"];

    // combos.js
    private static readonly Dictionary<string, string> CtrlActions = new()
    {
        ["a"] = "first",
        ["b"] = "backward",
        ["c"] = "cancel",
        ["d"] = "deleteForward",
        ["e"] = "last",
        ["f"] = "forward",
        ["g"] = "reset",
        ["i"] = "tab",
        ["k"] = "cutForward",
        ["l"] = "reset",
        ["n"] = "newItem",
        ["m"] = "cancel",
        ["j"] = "submit",
        ["p"] = "search",
        ["r"] = "remove",
        ["s"] = "save",
        ["u"] = "undo",
        ["w"] = "cutLeft",
        ["x"] = "toggleCursor",
        ["v"] = "paste"
    };

    private static readonly Dictionary<string, string> ShiftActions = new()
    {
        ["up"] = "shiftUp",
        ["down"] = "shiftDown",
        ["left"] = "shiftLeft",
        ["right"] = "shiftRight",
        ["tab"] = "prev"
    };

    private static readonly Dictionary<string, string> OptionActions = new()
    {
        ["b"] = "backward",
        ["f"] = "forward",
        ["d"] = "cutRight",
        ["left"] = "cutLeft",
        ["up"] = "altUp",
        ["down"] = "altDown"
    };

    private static readonly Dictionary<string, string> KeyActions = new()
    {
        ["pageup"] = "pageUp",
        ["pagedown"] = "pageDown",
        ["home"] = "home",
        ["end"] = "end",
        ["cancel"] = "cancel",
        ["delete"] = "deleteForward",
        ["backspace"] = "delete",
        ["down"] = "down",
        ["enter"] = "submit",
        ["escape"] = "cancel",
        ["left"] = "left",
        ["space"] = "space",
        ["number"] = "number",
        ["return"] = "submit",
        ["right"] = "right",
        ["tab"] = "next",
        ["up"] = "up"
    };

    [GeneratedRegex("^(?:\u001b)([a-zA-Z0-9])$")]
    private static partial Regex MetaKeyCode();

    [GeneratedRegex("^(?:\u001b+)(O|N|\\[|\\[\\[)(?:(\\d+)(?:;(\\d+))?([~^$])|(?:1;)?(\\d+)?([a-zA-Z]))")]
    private static partial Regex FnKey();

    /// <summary>enquirer's `keypress(s, event)`: derive the key from the raw sequence.</summary>
    public static KeyEvent Parse(string? input, KeyEvent? evt)
    {
        evt ??= new KeyEvent();
        KeyEvent key = evt.Clone();
        string original = input ?? string.Empty;
        key.Raw ??= original;
        key.Sequence = !string.IsNullOrEmpty(evt.Sequence) ? evt.Sequence : original;
        string s = original.Length > 0 ? original : key.Sequence;
        key.Sequence = !string.IsNullOrEmpty(key.Sequence) ? key.Sequence : s.Length > 0 ? s : key.Name;

        Match parts;
        if (s == "\r")
        {
            key.Raw = null;
            key.Name = "return";
        }
        else if (s == "\n")
        {
            key.Name = "enter";
        }
        else if (s == "\t")
        {
            key.Name = "tab";
        }
        else if (s is "\b" or "\u007f" or "\u001b\u007f" or "\u001b\b")
        {
            key.Name = "backspace";
            key.Meta = s[0] == '\u001b';
        }
        else if (s is "\u001b" or "\u001b\u001b")
        {
            key.Name = "escape";
            key.Meta = s.Length == 2;
        }
        else if (s is " " or "\u001b ")
        {
            key.Name = "space";
            key.Meta = s.Length == 2;
        }
        else if (string.CompareOrdinal(s, "\u001a") <= 0)
        {
            key.Name = ((char)(s[0] + 'a' - 1)).ToString();
            key.Ctrl = true;
        }
        else if (s.Length == 1 && s[0] >= '0' && s[0] <= '9')
        {
            key.Name = "number";
        }
        else if (s.Length == 1 && s[0] >= 'a' && s[0] <= 'z')
        {
            key.Name = s;
        }
        else if (s.Length == 1 && s[0] >= 'A' && s[0] <= 'Z')
        {
            key.Name = s.ToLowerInvariant();
            key.Shift = true;
        }
        else if ((parts = MetaKeyCode().Match(s)).Success)
        {
            key.Meta = true;
            key.Shift = parts.Groups[1].Value.Length == 1 && parts.Groups[1].Value[0] is >= 'A' and <= 'Z';
        }
        else if ((parts = FnKey().Match(s)).Success)
        {
            if (s.Length > 1 && s[0] == '\u001b' && s[1] == '\u001b')
            {
                key.Option = true;
            }

            string code = parts.Groups[1].Value + parts.Groups[2].Value + parts.Groups[4].Value + parts.Groups[6].Value;
            string modifierText = parts.Groups[3].Success && parts.Groups[3].Value.Length > 0 ? parts.Groups[3].Value
                : parts.Groups[5].Success && parts.Groups[5].Value.Length > 0 ? parts.Groups[5].Value : "1";
            int modifier = int.Parse(modifierText) - 1;
            key.Ctrl = (modifier & 4) != 0;
            key.Meta = (modifier & 10) != 0;
            key.Shift = (modifier & 1) != 0;
            key.Code = code;
            key.Name = KeyNames.GetValueOrDefault(code);
            key.Shift = ShiftCodes.Contains(code) || key.Shift;
            key.Ctrl = CtrlCodes.Contains(code) || key.Ctrl;
        }

        return key;
    }

    /// <summary>`keypress.action`: the method name the key triggers (null when none).</summary>
    public static KeyEvent Action(KeyEvent key)
    {
        if (key.Ctrl)
        {
            key.Action = key.Name is null ? null : CtrlActions.GetValueOrDefault(key.Name);

            return key;
        }

        if (key.Option)
        {
            key.Action = key.Name is null ? null : OptionActions.GetValueOrDefault(key.Name);

            return key;
        }

        if (key.Shift)
        {
            key.Action = key.Name is null ? null : ShiftActions.GetValueOrDefault(key.Name);

            return key;
        }

        key.Action = key.Name is null ? null : KeyActions.GetValueOrDefault(key.Name);

        return key;
    }

    /// <summary>
    /// Node's readline `emitKeys`: splits a chunk of input into key sequences. A lone escape at the end of the chunk is the
    /// escape key (Node emits it after its 500 ms escape-code timeout).
    /// </summary>
    public static IEnumerable<(string Sequence, KeyEvent Key)> EmitKeys(string chunk)
    {
        int position = 0;
        string? Next()
        {
            if (position >= chunk.Length)
            {
                return null;
            }

            int length = char.IsHighSurrogate(chunk[position]) && position + 1 < chunk.Length ? 2 : 1;
            string next = chunk.Substring(position, length);
            position += length;

            return next;
        }

        while (position < chunk.Length)
        {
            string ch = Next()!;
            string s = ch;
            bool escaped = false;
            var key = new KeyEvent();

            if (ch == "\u001b")
            {
                escaped = true;
                ch = Next() ?? string.Empty;
                s += ch;
                if (ch == "\u001b")
                {
                    ch = Next() ?? string.Empty;
                    s += ch;
                }
            }

            if (escaped && (ch is "O" or "["))
            {
                string code = ch;
                int modifier = 0;
                if (ch == "O")
                {
                    ch = Next() ?? string.Empty;
                    s += ch;
                    if (ch.Length == 1 && ch[0] is >= '0' and <= '9')
                    {
                        modifier = (ch[0] - '0') - 1;
                        ch = Next() ?? string.Empty;
                        s += ch;
                    }

                    code += ch;
                }
                else
                {
                    ch = Next() ?? string.Empty;
                    s += ch;
                    if (ch == "[")
                    {
                        code += ch;
                        ch = Next() ?? string.Empty;
                        s += ch;
                    }

                    int commandStart = s.Length - 1;
                    while (ch.Length == 1 && ch[0] is >= '0' and <= '9')
                    {
                        ch = Next() ?? string.Empty;
                        s += ch;
                    }

                    if (ch == ";")
                    {
                        ch = Next() ?? string.Empty;
                        s += ch;
                        while (ch.Length == 1 && ch[0] is >= '0' and <= '9')
                        {
                            ch = Next() ?? string.Empty;
                            s += ch;
                        }
                    }

                    string command = s[commandStart..];
                    Match match;
                    if ((match = Regex.Match(command, @"^(?:(\d\d?)(?:;(\d))?([~^$])|(\d{3}~))$")).Success)
                    {
                        if (match.Groups[4].Success && match.Groups[4].Value.Length > 0)
                        {
                            code += match.Groups[4].Value;
                        }
                        else
                        {
                            if (match.Groups[2].Success && match.Groups[2].Value.Length > 0)
                            {
                                modifier = int.Parse(match.Groups[2].Value) - 1;
                            }

                            code += match.Groups[1].Value + match.Groups[3].Value;
                        }
                    }
                    else if ((match = Regex.Match(command, @"^((?:\d;)?(\d))?([A-Za-z])$")).Success)
                    {
                        code += match.Groups[3].Value;
                        modifier = match.Groups[2].Success && match.Groups[2].Value.Length > 0 ? int.Parse(match.Groups[2].Value) - 1 : 0;
                    }
                    else
                    {
                        code += command;
                    }
                }

                key.Ctrl = (modifier & 4) != 0;
                key.Meta = (modifier & 10) != 0;
                key.Shift = (modifier & 1) != 0;
                key.Code = code;
                key.Name = KeyNames.GetValueOrDefault(code);
            }
            else if (ch == "\r")
            {
                key.Name = "return";
                key.Meta = escaped;
            }
            else if (ch == "\n")
            {
                key.Name = "enter";
                key.Meta = escaped;
            }
            else if (ch == "\t")
            {
                key.Name = "tab";
                key.Meta = escaped;
            }
            else if (ch is "\b" or "\u007f")
            {
                key.Name = "backspace";
                key.Meta = escaped;
            }
            else if (ch == "\u001b")
            {
                key.Name = "escape";
                key.Meta = escaped;
            }
            else if (ch == " ")
            {
                key.Name = "space";
                key.Meta = escaped;
            }
            else if (!escaped && ch.Length == 1 && ch[0] <= '\u001a')
            {
                key.Name = ((char)(ch[0] + 'a' - 1)).ToString();
                key.Ctrl = true;
            }
            else if (ch.Length == 1 && (char.IsAsciiLetterOrDigit(ch[0])))
            {
                key.Name = ch.ToLowerInvariant();
                key.Shift = char.IsAsciiLetterUpper(ch[0]);
                key.Meta = escaped;
            }
            else if (escaped)
            {
                key.Name = ch.Length > 0 ? null : "escape";
                key.Meta = true;
            }

            key.Sequence = s;
            if (s.Length != 0 && (key.Name is not null || escaped))
            {
                yield return (s, key);
            }
            else if (s.Length > 0 && (char.IsHighSurrogate(s[0]) ? 2 : 1) == s.Length)
            {
                yield return (s, key);
            }
        }
    }

}
