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
