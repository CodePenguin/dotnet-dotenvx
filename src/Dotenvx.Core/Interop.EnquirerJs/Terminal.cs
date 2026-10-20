using System.Text;

namespace Dotenvx.Interop.EnquirerJs;

/// <summary>The prompts' render stream: stderr, so stdout stays machine-readable (prompts.js `enquirerOptions`).</summary>
public sealed class ConsolePromptOutput : IPromptOutput
{
    public static ConsolePromptOutput Instance { get; } = new();

    public int? Columns => Query(() => Console.WindowWidth);

    public int? Rows => Query(() => Console.WindowHeight);

    public void Write(string text)
    {
        Console.Error.Write(text);
        Console.Error.Flush();
    }

    private static int? Query(Func<int> read)
    {
        if (Console.IsErrorRedirected)
        {
            return null;
        }

        try
        {
            int value = read();

            return value > 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <remarks>
/// The keys of stdin. A terminal yields one key at a time, as the escape sequence Node's tty stream would deliver; piped input
/// yields whatever the stream returns per read (a whole chunk), exactly as Node's 'data' event does, so a prompt that finishes
/// early drops the rest of its chunk like enquirer does.
/// </remarks>
public sealed class ConsoleKeySource : IKeySource
{
    private static readonly Stream Stream = Console.OpenStandardInput();

    private static readonly Decoder Decoder = new UTF8Encoding(false).GetDecoder();

    public static ConsoleKeySource Instance { get; } = new();

    public string? ReadChunk()
    {
        if (!Console.IsInputRedirected)
        {
            bool previous = Console.TreatControlCAsInput;
            Console.TreatControlCAsInput = true;
            try
            {
                string first = Sequence(Console.ReadKey(true));

                return first == "\u001b" ? first + AfterEscape() : first;
            }
            finally
            {
                Console.TreatControlCAsInput = previous;
            }
        }

        var buffer = new byte[65536];
        while (true)
        {
            int count = Stream.Read(buffer, 0, buffer.Length);
            if (count == 0)
            {
                return null;
            }

            char[] characters = new char[Encoding.UTF8.GetMaxCharCount(count)];
            int length = Decoder.GetChars(buffer, 0, count, characters, 0);
            if (length > 0)
            {
                return new string(characters, 0, length);
            }
        }
    }

    /// <summary>
    /// readline holds a lone escape for 500 ms (escapeCodeTimeout) in case more of an escape sequence follows; a key that arrives
    /// in that window is part of it (escape then ctrl-c is meta-ctrl-c, not a cancel).
    /// </summary>
    private static string AfterEscape()
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < deadline)
        {
            if (Console.KeyAvailable)
            {
                return Sequence(Console.ReadKey(true));
            }

            Thread.Sleep(5);
        }

        return string.Empty;
    }

    /// <summary>The bytes a terminal sends for a key (what Node's tty stream hands readline).</summary>
    private static string Sequence(ConsoleKeyInfo info)
    {
        int modifier = 1 + (info.Modifiers.HasFlag(ConsoleModifiers.Shift) ? 1 : 0) + (info.Modifiers.HasFlag(ConsoleModifiers.Alt) ? 2 : 0)
            + (info.Modifiers.HasFlag(ConsoleModifiers.Control) ? 4 : 0);

        string Cursor(string letter) => modifier == 1 ? "\u001b[" + letter : $"\u001b[1;{modifier}{letter}";
        string Tilde(int code) => modifier == 1 ? $"\u001b[{code}~" : $"\u001b[{code};{modifier}~";

        switch (info.Key)
        {
            case ConsoleKey.UpArrow: return Cursor("A");
            case ConsoleKey.DownArrow: return Cursor("B");
            case ConsoleKey.RightArrow: return Cursor("C");
            case ConsoleKey.LeftArrow: return Cursor("D");
            case ConsoleKey.Home: return Cursor("H");
            case ConsoleKey.End: return Cursor("F");
            case ConsoleKey.Insert: return Tilde(2);
            case ConsoleKey.Delete: return Tilde(3);
            case ConsoleKey.PageUp: return Tilde(5);
            case ConsoleKey.PageDown: return Tilde(6);
            case ConsoleKey.Escape: return "\u001b";
            case ConsoleKey.Enter: return "\r";
            case ConsoleKey.Tab: return info.Modifiers.HasFlag(ConsoleModifiers.Shift) ? "\u001b[Z" : "\t";
            case ConsoleKey.Backspace: return info.Modifiers.HasFlag(ConsoleModifiers.Alt) ? "\u001b\u007f" : "\u007f";
        }

        string text = info.KeyChar == '\0' ? string.Empty : info.KeyChar.ToString();

        return info.Modifiers.HasFlag(ConsoleModifiers.Alt) && text.Length > 0 && !info.Modifiers.HasFlag(ConsoleModifiers.Control)
            ? "\u001b" + text
            : text;
    }
}
