using System.Text.RegularExpressions;

namespace Dotenvx.Interop.EnquirerJs;

/// <remarks>
/// Reference source: enquirer lib/types/string.js (`StringPrompt`), lib/prompts/input.js and lib/prompts/password.js. Typing,
/// cursor movement and the clipboard behave as enquirer's; the password prompt only differs in how it formats the input.
/// </remarks>
internal class StringPrompt : Prompt
{
    public StringPrompt(PromptOptions options, string type)
        : base(options, type)
    {
        Initial = options.InitialText ?? string.Empty;
        if (((string)Initial).Length > 0)
        {
            CursorHide();
        }

        State.PrevCursor = 0;
    }

    protected string InitialText => (string?)Initial ?? string.Empty;

    protected override void Initialize()
    {
        if (Options.PresetInput is { } preset)
        {
            Input = preset;
            Cursor = preset.Length;
        }

        base.Initialize();
    }

    public override void Keypress(string? input, KeyEvent? key)
    {
        State.PrevKeypress = key;
        base.Keypress(input, key);
    }

    protected override bool Invoke(string? action, string? input, KeyEvent key)
    {
        if (base.Invoke(action, input, key))
        {
            return true;
        }

        switch (action)
        {
            case "delete":
                Delete();
                return true;
            case "deleteForward":
                DeleteForward();
                return true;
            case "cutForward":
                CutForward();
                return true;
            case "cutLeft":
                CutLeft();
                return true;
            case "paste":
                Paste();
                return true;
            case "toggleCursor":
                ToggleCursor();
                return true;
            case "first":
                Cursor = 0;
                Render();
                return true;
            case "last":
                Cursor = Input.Length - 1;
                Render();
                return true;
            case "next":
                Next();
                return true;
            case "prev":
                Prev();
                return true;
            case "backward":
            case "left":
                Left();
                return true;
            case "forward":
            case "right":
                Right();
                return true;
            case "reset":
                Reset();
                return true;
            default:
                return false;
        }
    }

    protected override void Dispatch(string? ch, KeyEvent key)
    {
        if (string.IsNullOrEmpty(ch) || key.Ctrl || key.Code is not null)
        {
            Alert();

            return;
        }

        Append(ch);
    }

    public void Reset()
    {
        Input = string.Empty;
        Value = string.Empty;
        Cursor = 0;
        Render();
    }

    public void Append(string ch)
    {
        string input = State.Input;
        int cursor = State.Cursor;
        Input = Slice(input, 0, cursor) + ch + Slice(input, cursor);
        Cursor += ch.Length;
        Render();
    }

    public void Delete()
    {
        string input = State.Input;
        int cursor = State.Cursor;
        if (cursor <= 0)
        {
            Alert();

            return;
        }

        Input = Slice(input, 0, cursor - 1) + Slice(input, cursor);
        Cursor--;
        Render();
    }

    public void DeleteForward()
    {
        string input = State.Input;
        int cursor = State.Cursor;
        if (cursor >= input.Length)
        {
            Alert();

            return;
        }

        Input = Slice(input, 0, cursor) + Slice(input, cursor + 1);
        Render();
    }

    public void CutForward()
    {
        int pos = Cursor;
        if (Input.Length <= pos)
        {
            Alert();

            return;
        }

        State.Clipboard.Add(Slice(Input, pos));
        Input = Slice(Input, 0, pos);
        Render();
    }

    public void CutLeft()
    {
        int pos = Cursor;
        if (pos == 0)
        {
            Alert();

            return;
        }

        string before = Slice(Input, 0, pos);
        string after = Slice(Input, pos);
        List<string> words = [.. before.Split(' ')];
        State.Clipboard.Add(words[^1]);
        words.RemoveAt(words.Count - 1);
        Input = string.Join(" ", words);
        Cursor = Input.Length;
        Input += after;
        Render();
    }

    public void Paste()
    {
        if (State.Clipboard.Count == 0)
        {
            Alert();

            return;
        }

        string text = State.Clipboard[^1];
        State.Clipboard.RemoveAt(State.Clipboard.Count - 1);

        // insert() renders and paste() renders again without awaiting, so enquirer's two async renders interleave: both format
        // the input (which hides the cursor) before either of them writes its frame
        string input = State.Input;
        int cursor = State.Cursor;
        Input = Slice(input, 0, cursor) + text + Slice(input, cursor);
        Cursor += text.Length;
        Frame first = Prepare();
        Frame second = Prepare();
        Flush(first);
        Flush(second);
    }

    public void ToggleCursor()
    {
        if (State.PrevCursor != 0)
        {
            Cursor = State.PrevCursor;
            State.PrevCursor = 0;
        }
        else
        {
            State.PrevCursor = Cursor;
            Cursor = 0;
        }

        Render();
    }

    public void Next()
    {
        string init = InitialText;
        if (init.Length == 0 || !init.StartsWith(Input, StringComparison.Ordinal))
        {
            Alert();

            return;
        }

        Input = init;
        Cursor = init.Length;
        Render();
    }

    public void Prev()
    {
        if (Input.Length == 0)
        {
            Alert();

            return;
        }

        Reset();
    }

    public void Right()
    {
        if (Cursor >= Input.Length)
        {
            Alert();

            return;
        }

        Cursor++;
        Render();
    }

    public void Left()
    {
        if (Cursor <= 0)
        {
            Alert();

            return;
        }

        Cursor--;
        Render();
    }

    protected override bool IsValue(object? value)
    {
        return value switch
        {
            null => false,
            string text => text.Length > 0,
            _ => true
        };
    }

    /// <summary>`format(input)`: the placeholder rendering while typing, the submitted style afterwards.</summary>
    protected virtual string Format(string input)
    {
        string initial = InitialText;
        if (!State.Submitted)
        {
            return Placeholder(input, initial, Cursor);
        }

        return Palette.Success.Apply(input.Length > 0 ? input : initial);
    }

    protected override string Format()
    {
        return Format(Primitive(Value));
    }

    /// <summary>enquirer lib/placeholder.js: the input with a simulated cursor and the rest of the initial value dimmed.</summary>
    private string Placeholder(string input, string initial, int pos)
    {
        CursorHide();
        Style style = Palette.Placeholder;
        Style inverse = Style.Inverse(Palette.Primary);
        string Blinker(string text) => inverse.Apply(Style.Black.Apply(text));
        string output = input;
        string reverse = Blinker(" ");

        if (pos == 0 && initial.Length == 0 && input.Length == 0)
        {
            return Blinker(" ");
        }

        if (pos == 0 && (input == initial || input.Length == 0))
        {
            return Blinker(initial.Length > 0 ? initial[..1] : string.Empty) + style.Apply(initial.Length > 0 ? initial[1..] : string.Empty);
        }

        bool placeholder = initial.Length > 0 && initial.StartsWith(input, StringComparison.Ordinal) && initial != input;
        string cursor = placeholder ? Blinker(initial[input.Length].ToString()) : reverse;
        if (pos != input.Length)
        {
            output = Slice(input, 0, pos) + Blinker(pos < input.Length ? input[pos].ToString() : string.Empty) + Slice(input, pos + 1);
            cursor = string.Empty;
        }

        if (placeholder)
        {
            string raw = Style.Unstyle(output + cursor);

            return output + cursor + style.Apply(Slice(initial, raw.Length));
        }

        return output + cursor;
    }

    private readonly record struct Frame(int Size, string Text);

    /// <summary>Everything a render does before it writes: reads the size, builds the frame (formatting can hide the cursor).</summary>
    private Frame Prepare()
    {
        int size = State.Size;
        string prefix = Prefix();
        string separator = Separator();
        string message = Message();
        string prompt = string.Join(" ", new[] { prefix, message, separator }.Where(part => part.Length > 0));
        State.Prompt = prompt;
        string header = string.Empty;
        string output = Format();
        string help = ErrorText().Length > 0 ? ErrorText() : Hint();
        string footer = string.Empty;
        if (help.Length > 0 && !output.Contains(help, StringComparison.Ordinal))
        {
            output += " " + help;
        }

        prompt += " " + output;

        return new Frame(size, string.Join("\n", new[] { header, prompt, footer }.Where(part => part.Length > 0)));
    }

    private void Flush(Frame frame)
    {
        Clear(frame.Size);
        Write(frame.Text);
        Restore();
    }

    public override void Render()
    {
        Flush(Prepare());
    }
}

/// <remarks>Reference source: enquirer lib/prompts/password.js. The input shows as asterisks (muted while typing, primary once submitted).</remarks>
internal sealed class PasswordPrompt : StringPrompt
{
    public PasswordPrompt(PromptOptions options)
        : base(options, "password")
    {
        CursorShow();
    }

    protected override string Format(string input)
    {
        if (!KeyPressed)
        {
            return string.Empty;
        }

        Style color = State.Submitted ? Palette.Primary : Palette.Muted;

        return color.Apply(new string('*', input.Length));
    }

    protected override string Format()
    {
        return Format(Input);
    }
}

/// <remarks>Reference source: enquirer lib/prompts/input.js (without the history store, which dotenvx never configures).</remarks>
internal sealed class InputPrompt(PromptOptions options) : StringPrompt(options, "input");

/// <remarks>Reference source: enquirer lib/types/boolean.js and lib/prompts/confirm.js.</remarks>
internal sealed class ConfirmPrompt : Prompt
{
    private readonly string _default;

    public ConfirmPrompt(PromptOptions options)
        : base(options, "confirm")
    {
        CursorHide();
        Initial = options.InitialBool;
        _default = options.InitialBool ? "(Y/n)" : "(y/N)";
    }

    // the input starts as the boolean initial value (false is falsy but still renders as "false") until a key replaces it
    private bool _booleanInput;

    protected override bool InputIsTruthy => _booleanInput ? State.Input == "true" : State.Input.Length > 0;

    protected override void Initialize()
    {
        Input = Cast(Initial) ? "true" : "false";
        _booleanInput = true;
        base.Initialize();
    }

    private static bool IsTrue(object? input)
    {
        return Regex.IsMatch(Primitive(input), "^[ty1]", RegexOptions.IgnoreCase);
    }

    private static bool IsFalse(object? input)
    {
        return Regex.IsMatch(Primitive(input), "^[fn0]", RegexOptions.IgnoreCase);
    }

    private static bool Cast(object? input)
    {
        return IsTrue(input);
    }

    protected override bool IsValue(object? value)
    {
        return value is string or bool && (IsTrue(value) || IsFalse(value));
    }

    protected override void Dispatch(string? ch, KeyEvent key)
    {
        if (ch is null || !IsValue(ch))
        {
            Alert();

            return;
        }

        Input = ch;
        _booleanInput = false;
        Submit();
    }

    public override object? Value
    {
        get => Cast(base.Value);
        set => base.Value = value;
    }

    public override void Render()
    {
        int size = State.Size;
        string prefix = Prefix();
        string separator = Separator();
        string message = Message();
        string hint = Palette.Muted.Apply(_default);
        string promptLine = string.Join(" ", new[] { prefix, message, hint, separator }.Where(part => part.Length > 0));
        State.Prompt = promptLine;
        string header = string.Empty;
        bool value = Cast(State.Input);
        Value = value;
        string text = value ? "true" : "false";
        string output = !State.Submitted ? Palette.Primary.Apply(text) : Palette.Success.Apply(text);
        string help = ErrorText().Length > 0 ? ErrorText() : string.Empty;
        string footer = string.Empty;
        if (help.Length > 0 && !promptLine.Contains(help, StringComparison.Ordinal))
        {
            output += " " + help;
        }

        promptLine += " " + output;

        Clear(size);
        Write(string.Join("\n", new[] { header, promptLine, footer }.Where(part => part.Length > 0)));
        Restore();
    }
}
