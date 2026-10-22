using System.Text;

namespace Dotenvx.Interop.EnquirerJs;

/// <summary>Where a prompt renders (enquirer's `stdout`; dotenvx points it at stderr so stdout stays machine-readable).</summary>
internal interface IPromptOutput
{
    int? Columns { get; }

    int? Rows { get; }

    void Write(string text);
}

/// <summary>Where a prompt reads keys from (enquirer's `stdin`): chunks of input, null at the end of the stream.</summary>
internal interface IKeySource
{
    string? ReadChunk();
}

/// <summary>The input ended before the prompt was answered: upstream's promise never settles and Node exits quietly.</summary>
internal sealed class PromptInputEndedException : Exception;

/// <summary>The prompt was cancelled (Ctrl+C, Escape on custody menus); upstream rejects with a non-Error value.</summary>
internal sealed class PromptCancelled : Exception;

/// <summary>Mutable view of a choice (enquirer normalises user choices into these).</summary>
internal sealed class PromptChoice
{
    public string Name { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? Hint { get; set; }

    public bool Disabled { get; set; }

    public string? Role { get; set; }

    public bool Action { get; set; }

    public bool? Enabled { get; set; }

    public int Index { get; set; }

    public string Indent { get; set; } = string.Empty;

    public int Level { get; set; } = 1;

    public string Pad { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    public int Cursor { get; set; }

    public string? Indicator { get; set; }

    public bool Normalized { get; set; }

    public bool NewChoice { get; set; }
}

/// <summary>A choice as prompts.js hands it to enquirer (`choicesForSelect`).</summary>
internal sealed record ChoiceSpec(string Name, string? Message = null, string? Role = null, bool? Disabled = null, string? DisabledHint = null);

/// <summary>The options the dotenvx prompts pass to enquirer.</summary>
internal sealed class PromptOptions
{
    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<ChoiceSpec> Choices { get; set; } = [];

    /// <summary>`initial`: a choice name (select), a list of names (multiselect) or null.</summary>
    public string? InitialName { get; set; }

    public IReadOnlyList<string>? InitialNames { get; set; }

    public string? InitialText { get; set; }

    public bool InitialBool { get; set; }

    public string? Pointer { get; set; }

    public string? PrefixPending { get; set; }

    public string? PrefixSubmitted { get; set; }

    public string? PrefixCancelled { get; set; }

    public string? SeparatorPending { get; set; }

    public string? SeparatorSubmitted { get; set; }

    public string? SeparatorCancelled { get; set; }

    public string? SubmitLabel { get; set; }

    public Func<IReadOnlyList<string>, string>? SubmitLabelFunction { get; set; }

    public string? BackValue { get; set; }

    public IPromptOutput Output { get; set; } = null!;

    public IKeySource Input { get; set; } = null!;
}

/// <remarks>
/// Reference source: enquirer lib/prompt.js and lib/state.js. A line-for-line port of the base prompt: the same render loop
/// (write the frame, remember it in a buffer, restore the cursor), the same clearing of the previous frame, the same submit and
/// cancel sequences, so the bytes written to the terminal are the bytes Node writes. enquirer's promises are sequential here,
/// so everything is synchronous.
/// </remarks>
internal abstract class Prompt
{
    protected Prompt(PromptOptions options, string type)
    {
        Options = options;
        Type = type;
        Symbols = new Symbols();
        ApplySymbolOverrides();
        State = new PromptState()
        {
            Width = ColumnsOf(options.Output)
        };
        Stdout = options.Output;
        Stdin = options.Input;
    }

    public PromptOptions Options { get; }

    public string Type { get; }

    public PromptState State { get; }

    public Symbols Symbols { get; }

    public IPromptOutput Stdout { get; }

    public IKeySource Stdin { get; }

    /// <summary>`this.initial`: kept as the JS value (number, list or string) because the cursor arithmetic looks at its type.</summary>
    public object? Initial { get; set; }

    public bool KeyPressed { get; protected set; }

    public bool IsClosed => State.Closed;

    protected object? Result { get; set; }

    protected bool Finished { get; set; }

    protected bool Cancelled { get; set; }

    // enquirer registers one 'close' listener (and one exit hook) per cursorHide() call, each of which shows the cursor again
    private int _hides;

    private bool _stopped;

    protected static int? ColumnsOf(IPromptOutput output)
    {
        return output.Columns;
    }

    public int Width
    {
        get
        {
            int columns = Stdout.Columns is > 0 ? Stdout.Columns.Value : 80;

            return OperatingSystem.IsWindows() ? columns - 1 : columns;
        }
    }

    public int Height => Stdout.Rows is { } rows && rows > 0 ? rows : 25;

    public string Input
    {
        get => State.Input;
        set => State.Input = value;
    }

    public int Cursor
    {
        get => State.Cursor;
        set => State.Cursor = value;
    }

    /// <summary>`prompt.value` (Prompt base): the first of state.value / state.input that is a value, else the initial.</summary>
    public virtual object? Value
    {
        get
        {
            object? found = new[] { State.Value, State.Input }.FirstOrDefault(IsValue);

            return IsValue(found) ? found : Initial;
        }
        set => State.Value = value;
    }

    private void ApplySymbolOverrides()
    {
        static void Override(Dictionary<string, string> target, string key, string? value)
        {
            if (value is not null)
            {
                target[key] = value;
            }
        }

        Override(Symbols.Prefix, "pending", Options.PrefixPending);
        Override(Symbols.Prefix, "submitted", Options.PrefixSubmitted);
        Override(Symbols.Prefix, "cancelled", Options.PrefixCancelled);
        Override(Symbols.Separator, "pending", Options.SeparatorPending);
        Override(Symbols.Separator, "submitted", Options.SeparatorSubmitted);
        Override(Symbols.Separator, "cancelled", Options.SeparatorCancelled);
    }

    // ---- keypress ----------------------------------------------------------------------------------------------------

    /// <summary>`prompt.keypress(input, event)`.</summary>
    public virtual void Keypress(string? input, KeyEvent? evt)
    {
        KeyPressed = true;
        KeyEvent key = KeypressDecoder.Action(KeypressDecoder.Parse(input, evt));
        State.Keypress = key;
        if (!Invoke(key.Action, input, key))
        {
            Dispatch(input, key);
        }
    }

    /// <summary>`this[key.action]`: runs the method of that name when the prompt has one.</summary>
    protected virtual bool Invoke(string? action, string? input, KeyEvent key)
    {
        switch (action)
        {
            case "submit":
                Submit();

                return true;
            case "cancel":
                Cancel();

                return true;
            default:
                return false;
        }
    }

    protected abstract void Dispatch(string? input, KeyEvent key);

    public void Alert()
    {
        State.Alert = null;
        Stdout.Write(Ansi.Bell);
    }

    // ---- output ------------------------------------------------------------------------------------------------------

    public void CursorHide()
    {
        Stdout.Write(Ansi.Hide);
        _hides++;
    }

    public void CursorShow()
    {
        Stdout.Write(Ansi.Show);
    }

    /// <summary>Process exit while the cursor is hidden: enquirer's `onExit` hook shows it again.</summary>
    public void ReleaseOnExit()
    {
        for (int i = 0; i < _hides && !State.Closed; i++)
        {
            CursorShow();
        }
    }

    public void Write(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Stdout.Write(text);
        State.Buffer += text;
    }

    public void Clear(int lines = 0)
    {
        string buffer = State.Buffer;
        State.Buffer = string.Empty;
        if (buffer.Length == 0 && lines == 0)
        {
            return;
        }

        Stdout.Write(Ansi.Down(lines) + Ansi.Clear(buffer, Width));
    }

    public void Restore()
    {
        if (State.Closed)
        {
            return;
        }

        Sections sections = GetSections();
        int size = State.Size = sections.Rest.Length;
        string? codes = Ansi.Restore(sections.After, Cursor, Primitive(Initial), Primitive(Input), sections.Prompt, size,
            Primitive(Value));
        if (!string.IsNullOrEmpty(codes))
        {
            Stdout.Write(codes);
        }
    }

    /// <summary>enquirer's `isPrimitive(x) ? String(x) : ''`.</summary>
    protected static string Primitive(object? value)
    {
        return value switch
        {
            string text => text,
            int number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            _ => string.Empty
        };
    }

    public readonly record struct Sections(string Header, string Prompt, string After, string[] Rest, string Last);

    public Sections GetSections()
    {
        string prompt = Style.StripAnsi(State.Prompt);
        string buf = Style.StripAnsi(State.Buffer);
        int index = buf.IndexOf(prompt, StringComparison.Ordinal);
        string header = Slice(buf, 0, index);
        string rest = Slice(buf, index);
        string[] lines = rest.Split('\n');
        string first = lines[0];
        string last = lines[^1];
        string promptLine = prompt + (InputIsTruthy ? " " + State.Input : string.Empty);
        int length = promptLine.Length;
        string after = length < first.Length ? Slice(first, length + 1) : string.Empty;

        return new Sections(header, first, after, lines[1..], last);
    }

    /// <summary>Whether `state.input` is truthy in JavaScript (a boolean input of false has the text "false" but is falsy).</summary>
    protected virtual bool InputIsTruthy => State.Input.Length > 0;

    /// <summary>JavaScript `String.prototype.slice`.</summary>
    protected static string Slice(string text, int start, int? end = null)
    {
        int length = text.Length;
        int from = start < 0 ? Math.Max(length + start, 0) : Math.Min(start, length);
        int to = end is null ? length : end < 0 ? Math.Max(length + end.Value, 0) : Math.Min(end.Value, length);

        return from >= to ? string.Empty : text[from..to];
    }

    // ---- lifecycle ---------------------------------------------------------------------------------------------------

    public virtual void Submit()
    {
        State.Submitted = true;
        State.Validating = true;

        object? result = State.Error.Length > 0 ? State.Error : Validate(Value);
        if (result is not true)
        {
            string error = "\n" + Symbols.Pointer + " " + (result is string text ? text.Trim() : "Invalid input");
            State.Error = "\n" + Palette.Danger.Apply(error);
            State.Submitted = false;
            Render();
            Alert();
            State.Validating = false;
            State.Error = string.Empty;

            return;
        }

        State.Validating = false;
        Render();
        Close();

        Value = FinalValue(Value);
        Result = Value;
        Finished = true;
    }

    protected virtual object? FinalValue(object? value)
    {
        return value;
    }

    public virtual void Cancel()
    {
        State.Cancelled = State.Submitted = true;

        Render();
        Close();

        Cancelled = true;
        Finished = true;
    }

    public void Close()
    {
        State.Closed = true;

        try
        {
            Sections sections = GetSections();
            int lines = (int)Math.Ceiling(sections.Prompt.Length / (double)Width);
            if (sections.Rest.Length > 0)
            {
                Write(Ansi.Down(sections.Rest.Length));
            }

            Write(new string('\n', lines));
        }
        catch
        {
            // do nothing
        }

        for (int i = 0; i < _hides; i++)
        {
            CursorShow();
        }

        _stopped = true;
    }

    protected virtual bool Validate(object? value)
    {
        return true;
    }

    protected virtual bool IsValue(object? value)
    {
        return value is not null && !(value is string text && text.Length == 0);
    }

    public abstract void Render();

    protected virtual void Initialize()
    {
        Render();
    }

    /// <summary>Starts the prompt, feeds it keys until it is submitted or cancelled and returns its value.</summary>
    public object? Run()
    {
        Initialize();
        while (!Finished)
        {
            string? chunk = Stdin.ReadChunk();
            if (chunk is null)
            {
                ReleaseOnExit();

                throw new PromptInputEndedException();
            }

            foreach ((string sequence, KeyEvent key) in KeypressDecoder.EmitKeys(chunk))
            {
                if (_stopped)
                {
                    break;
                }

                // the queue hands enquirer's own parse of the key to the prompt, which parses it again
                // readline hands the raw text only for unescaped keys
                Keypress(sequence.StartsWith('') ? null : sequence, KeypressDecoder.Parse(sequence, key));
            }
        }

        if (Cancelled)
        {
            throw new PromptCancelled();
        }

        return Result;
    }

    // ---- elements ----------------------------------------------------------------------------------------------------

    public string Prefix()
    {
        string element = Symbols.Prefix.GetValueOrDefault(State.Status) ?? Symbols.Prefix["pending"];
        if (!Style.HasColor(element))
        {
            return Palette.ForStatus(State.Status).Apply(element);
        }

        return element;
    }

    public string Message()
    {
        string message = Options.Message;

        return Style.HasColor(message) ? message : Palette.Strong.Apply(message);
    }

    public virtual string Separator()
    {
        string element = Symbols.Separator.GetValueOrDefault(State.Status) ?? Symbols.Separator["pending"];

        return Style.HasColor(element) ? element : Palette.Muted.Apply(element);
    }

    /// <summary>`prompt.hint()`: nothing in the dotenvx prompts (no `hint` option), kept for the render pipeline.</summary>
    public virtual string Hint()
    {
        return string.Empty;
    }

    public string ErrorText()
    {
        return !State.Submitted ? State.Error : string.Empty;
    }

    /// <summary>`prompt.format(value)`.</summary>
    protected virtual string Format()
    {
        return Primitive(Value);
    }
}

/// <summary>enquirer's State: the mutable facts a prompt renders from.</summary>
internal sealed class PromptState
{
    public string Input { get; set; } = string.Empty;

    public int Cursor { get; set; }

    public int Index { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public string Buffer { get; set; } = string.Empty;

    public int? Width { get; set; }

    public int Size { get; set; }

    public bool Cancelled { get; set; }

    public bool Submitted { get; set; }

    public bool Closed { get; set; }

    public bool Validating { get; set; }

    public string Error { get; set; } = string.Empty;

    public object? Value { get; set; }

    public object? Alert { get; set; }

    public KeyEvent? Keypress { get; set; }

    public int? Limit { get; set; }

    public List<PromptChoice> Choices { get; set; } = [];

    public List<PromptChoice> AllChoices { get; set; } = [];

    public List<PromptChoice>? Visible { get; set; }

    public int PrevCursor { get; set; }

    public List<string> Clipboard { get; } = [];

    public KeyEvent? PrevKeypress { get; set; }

    public string Status => Cancelled ? "cancelled" : Submitted ? "submitted" : "pending";
}
