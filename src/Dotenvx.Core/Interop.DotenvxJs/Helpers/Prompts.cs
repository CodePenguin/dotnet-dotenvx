using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>Raised when the user cancels a prompt (Ctrl+C); upstream's `PROMPT_CANCELLED` error.</summary>
public class PromptCancelledException() : Exception("prompt cancelled");

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/prompts.js (`password`). Upstream renders through enquirer onto stderr; the
/// rendering here is the same shape (`⊡ passphrase = ***`, then the line is cleared) but not byte-identical (enquirer
/// emits per-keypress cursor and colour sequences). Verified live against Node: with piped stdin the answer is the first
/// line; stdin ending before Enter leaves the promise pending and Node exits 0 silently, which is the `null` result.
/// </remarks>
public static class Prompts
{
    private static readonly Func<string, string, string?> DefaultPassword = ReadPassword;

    /// <summary>(message, prefix) => the entered text, or null when input ended before Enter. Throws <see cref="PromptCancelledException" />.</summary>
    public static Func<string, string, string?> Password { get; internal set; } = DefaultPassword;

    internal static void ResetToDefaults()
    {
        Password = DefaultPassword;
        Select = DefaultSelect;
    }

    /// <summary>A choice for <see cref="Select" />: the text shown and the value returned.</summary>
    public record Choice(string Name, string Value);

    private static readonly Func<string, IReadOnlyList<Choice>, string?> DefaultSelect = ReadSelection;

    /// <summary>(message, choices) => the chosen value, or null when input ended before Enter. Throws <see cref="PromptCancelledException" />.</summary>
    public static Func<string, IReadOnlyList<Choice>, string?> Select { get; internal set; } = DefaultSelect;

    // built from char codes: this file must not contain literal escape characters (see AGENTS.md tooling cautions)
    private static readonly string KeyEscape = ((char)27).ToString();
    private static readonly string KeyDown = KeyEscape + "[B";
    private static readonly string KeyUp = KeyEscape + "[A";
    private static readonly string KeyCtrlC = ((char)3).ToString();
    private static readonly string KeyEnter = ((char)10).ToString();
    private static readonly string KeyReturn = ((char)13).ToString();

    private static string? ReadSelection(string message, IReadOnlyList<Choice> choices)
    {
        // enquirer select (verified live against piped stdin): Enter submits the highlighted choice, the down/up arrows
        // move (wrapping), a digit jumps to that index, anything else is ignored; stdin ending first leaves the promise
        // pending (the process exits 0 silently) and Ctrl+C cancels
        int index = 0;
        Render(message, choices, index);
        bool redirected = Console.IsInputRedirected;
        if (redirected)
        {
            Console.InputEncoding = new UTF8Encoding(false);
        }

        while (true)
        {
            string key;
            if (redirected)
            {
                int next = Console.In.Read();
                if (next == -1)
                {
                    return null;
                }

                key = ((char)next).ToString();
                if (next == 0x1b && Console.In.Peek() == '[')
                {
                    Console.In.Read();
                    key = KeyEscape + "[" + (char)Console.In.Read();
                }
            }
            else
            {
                ConsoleKeyInfo info = Console.ReadKey(true);
                key = info.Key switch
                {
                    ConsoleKey.DownArrow => KeyDown,
                    ConsoleKey.UpArrow => KeyUp,
                    ConsoleKey.Enter => KeyEnter,
                    ConsoleKey.C when info.Modifiers.HasFlag(ConsoleModifiers.Control) => KeyCtrlC,
                    _ => info.KeyChar.ToString()
                };
            }

            if (key == KeyCtrlC)
            {
                throw new PromptCancelledException();
            }

            if (key == KeyEnter || key == KeyReturn)
            {
                Console.Error.WriteLine();
                return choices[index].Value;
            }

            if (key == KeyDown)
            {
                index = (index + 1) % choices.Count;
            }
            else if (key == KeyUp)
            {
                index = (index - 1 + choices.Count) % choices.Count;
            }
            else if (key.Length == 1 && char.IsDigit(key[0]) && key[0] - '0' < choices.Count)
            {
                index = key[0] - '0';
            }

            Render(message, choices, index);
        }
    }

    private static void Render(string message, IReadOnlyList<Choice> choices, int index)
    {
        Console.Error.WriteLine($"? {message}");
        for (int i = 0; i < choices.Count; i++)
        {
            Console.Error.WriteLine($"{(i == index ? "❯" : " ")} {choices[i].Name}");
        }
    }

    private static string? ReadPassword(string message, string prefix)
    {
        Console.Error.Write($"{prefix} {message} = ");

        var typed = new StringBuilder();
        string? answer = null;

        if (Console.IsInputRedirected)
        {
            // Node reads piped stdin as UTF-8; .NET defaults to the OEM code page on Windows
            Console.InputEncoding = new UTF8Encoding(false);
            int next;
            while ((next = Console.In.Read()) != -1)
            {
                if (next == '\n')
                {
                    answer = typed.ToString();
                    break;
                }

                if (next == '\r')
                {
                    continue;
                }

                if (next is 0x7f or 0x08)
                {
                    if (typed.Length > 0)
                    {
                        typed.Length--;
                    }

                    continue;
                }

                if (next == 0x03)
                {
                    ClearLine();
                    throw new PromptCancelledException();
                }

                typed.Append((char)next);
            }
        }
        else
        {
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    answer = typed.ToString();
                    break;
                }

                if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    ClearLine();
                    throw new PromptCancelledException();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (typed.Length > 0)
                    {
                        typed.Length--;
                        Console.Error.Write("\b \b");
                    }

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    typed.Append(key.KeyChar);
                    Console.Error.Write('*');
                }
            }
        }

        if (answer is not null)
        {
            Console.Error.Write(new string('*', answer.Length));
            Console.Error.WriteLine();
        }

        // prompts.js clearLastLine: move up one line and erase it
        ClearLine(answer is not null);

        return answer;
    }

    private static void ClearLine(bool moveUp = true)
    {
        Console.Error.Write(moveUp ? "\u001b[1A\u001b[2K" : "\r\u001b[2K");
    }
}
