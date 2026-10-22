namespace Dotenvx.Interop.EnquirerJs;

/// <remarks>
/// Reference source: enquirer lib/types/array.js (`ArrayPrompt`), lib/prompts/select.js and lib/prompts/multiselect.js. One
/// class stands for the three because only `Select` and `MultiSelect` are instantiated (`Multiple` is what `MultiSelect`
/// adds). Member names follow the JS methods so a change upstream can be found by name.
/// </remarks>
internal class SelectPrompt : Prompt
{
    private readonly List<PromptChoice> _normalized = [];

    private bool _initialDone;

    private int? _limit;

    private int _longest;

    public SelectPrompt(PromptOptions options, bool multiple = false, string type = "select")
        : base(options, type)
    {
        CursorHide();
        Multiple = multiple;
        Initial = options.InitialNames is { } names ? names : options.InitialName is { } name ? name : (object)0;
        EmptyError = "No items were selected";
    }

    public bool Multiple { get; }

    public string EmptyError { get; }

    public int MaxSelected { get; } = int.MaxValue;

    /// <summary>`options.pointer` (the checklist sets one; plain selects use the theme's pointer).</summary>
    protected virtual string? PointerOption => Options.Pointer;

    // ---- initialisation ----------------------------------------------------------------------------------------------

    protected override void Initialize()
    {
        Reset();
        base.Initialize();
    }

    protected virtual IEnumerable<ChoiceSpec> ChoiceSpecs => Options.Choices;

    public void Reset()
    {
        State.AllChoices = [];
        State.Choices = [];

        if (_normalized.Count == 0)
        {
            int index = 0;
            foreach (ChoiceSpec spec in ChoiceSpecs)
            {
                _normalized.Add(ToChoice(spec, index++));
            }
        }

        Choices = [.. _normalized];
        foreach (PromptChoice choice in Choices)
        {
            choice.Enabled = false;
        }

        if (Selectable.Count == 0)
        {
            throw new InvalidOperationException("At least one choice must be selectable");
        }

        string? initialName = Options.InitialName;
        if (Options.InitialNames is { } names)
        {
            foreach (string name in names)
            {
                Enable(Find(name)!);
            }

            Render();
        }
        else if (initialName is not null)
        {
            int index = FindIndex(initialName);
            if (index > -1)
            {
                Index = Math.Max(0, Math.Min(index, Choices.Count));
                Enable(Find(Index)!);
            }
        }

        if (IsDisabled(Focused))
        {
            Down();
        }
    }

    protected virtual PromptChoice ToChoice(ChoiceSpec spec, int index)
    {
        var choice = new PromptChoice { Name = spec.Name, Message = spec.Message ?? spec.Name, Role = spec.Role };
        if (spec.Disabled is true)
        {
            choice.Disabled = true;
            choice.Hint = spec.DisabledHint ?? "(disabled)";
        }

        ApplyRole(choice);

        choice.Name = choice.Name.Length > 0 ? choice.Name : choice.Message;
        choice.Message = choice.Message.Length > 0 ? choice.Message : choice.Name;
        choice.Value = !string.IsNullOrEmpty(choice.Value) ? choice.Value : choice.Name;
        choice.Input = string.Empty;
        choice.Index = index;
        choice.Cursor = 0;
        choice.Level = 1;
        choice.Indent = string.Empty;
        choice.Enabled = Multiple && !IsDisabled(choice) && IsSelected(choice);
        if (!IsDisabled(choice))
        {
            _longest = Math.Max(_longest, Style.StripAnsi(choice.Message).Length);
        }

        choice.Normalized = true;

        return choice;
    }

    /// <summary>enquirer lib/roles.js: the roles a choice can have.</summary>
    private void ApplyRole(PromptChoice choice)
    {
        switch (choice.Role)
        {
            case null or "" or "default" or "option" or "spacer":
                break;
            case "heading":
                choice.Disabled = true;
                choice.Hint ??= string.Empty;
                choice.Indicator ??= " ";
                break;
            case "separator":
                choice.Disabled = true;
                choice.Indicator ??= " ";
                choice.Message = choice.Message.Length > 0 ? choice.Message : string.Concat(Enumerable.Repeat(Symbols.Line, 5));
                break;
            default:
                throw new InvalidOperationException($"{choice.Role} role is not implemented yet");
        }
    }

    // ---- choices -----------------------------------------------------------------------------------------------------

    public List<PromptChoice> Choices
    {
        get
        {
            foreach (PromptChoice choice in State.Choices)
            {
                if (IsDisabled(choice))
                {
                    choice.Enabled = null;
                }
            }

            return State.Choices;
        }
        set
        {
            State.AllChoices ??= [];
            State.Choices = value;

            foreach (PromptChoice choice in value)
            {
                if (!State.AllChoices.Any(item => item.Name == choice.Name))
                {
                    State.AllChoices.Add(choice);
                }
            }

            if (!_initialDone && (Options.InitialName is not null || Options.InitialNames is not null))
            {
                _initialDone = true;
                if (Initial is string initialName)
                {
                    PromptChoice? choice = Find(initialName);
                    if (choice is not null)
                    {
                        Initial = choice.Index;
                        Focus(choice, true);
                    }
                }
            }
        }
    }

    public List<PromptChoice> Visible => [.. (State.Visible ?? Choices).Take(Limit)];

    public int Limit
    {
        get
        {
            int limit = State.Limit is > 0 ? State.Limit.Value : _limit is > 0 ? _limit.Value : Choices.Count;

            return Math.Min(limit, Height);
        }
        set => State.Limit = value;
    }

    public int Index
    {
        get => Math.Max(0, State.Index);
        set => State.Index = value;
    }

    public PromptChoice? Focused
    {
        get
        {
            PromptChoice? choice = Index < Choices.Count ? Choices[Index] : null;
            if (choice is not null && State.Submitted && !Multiple)
            {
                choice.Enabled = true;
            }

            return choice;
        }
    }

    public List<PromptChoice> Selectable => [.. Choices.Where(choice => !IsDisabled(choice))];

    /// <summary>`get selected`: the enabled choices for a multiselect, the focused choice otherwise.</summary>
    public virtual List<PromptChoice> Selected => Multiple ? Enabled : Focused is { } focused ? [focused] : [];

    public List<PromptChoice> Enabled => [.. (Multiple ? State.AllChoices : Choices).Where(choice => IsEnabled(choice))];

    public PromptChoice? Find(string name)
    {
        for (int i = 0; i < Choices.Count; i++)
        {
            if (Choices[i].Name == name)
            {
                return Choices[i];
            }
        }

        return null;
    }

    public PromptChoice? Find(int position)
    {
        return position >= 0 && position < Choices.Count ? Choices[position] : null;
    }

    public int FindIndex(string name)
    {
        PromptChoice? choice = Find(name);

        return choice is null ? -1 : Choices.IndexOf(choice);
    }

    public bool IsDisabled(PromptChoice? choice)
    {
        return choice is not null && (choice.Disabled || choice.Role == "heading");
    }

    public bool IsEnabled(PromptChoice choice)
    {
        return choice.Enabled == true && !IsDisabled(choice);
    }

    private bool IsChoice(PromptChoice choice, object? value)
    {
        return value switch
        {
            string text => choice.Name == text || (int.TryParse(text, out int number) && choice.Index == number),
            int number => choice.Index == number,
            _ => false
        };
    }

    private bool IsSelected(PromptChoice choice)
    {
        return Initial is IReadOnlyList<string> names ? names.Any(name => IsChoice(choice, name)) : IsChoice(choice, Initial);
    }

    private bool AllChoicesAreDisabled()
    {
        return Choices.All(IsDisabled);
    }

    // ---- actions -----------------------------------------------------------------------------------------------------

    protected override bool Invoke(string? action, string? input, KeyEvent key)
    {
        if (base.Invoke(action, input, key))
        {
            return true;
        }

        return action is not null && Method(action, input, key);
    }

    /// <summary>`this[name]()` for the prompt methods keys can reach.</summary>
    protected virtual bool Method(string name, string? input, KeyEvent key)
    {
        switch (name)
        {
            case "up":
                Up();
                return true;
            case "down":
                Down();
                return true;
            case "left":
                Left();
                return true;
            case "right":
                Right();
                return true;
            case "first":
                First();
                return true;
            case "last":
                Last();
                return true;
            case "home":
                Home();
                return true;
            case "end":
                End();
                return true;
            case "prev":
                Prev();
                return true;
            case "next":
                Next();
                return true;
            case "pageUp":
                PageUp();
                return true;
            case "pageDown":
                PageDown();
                return true;
            case "shiftUp":
                ScrollUp(Index);
                return true;
            case "shiftDown":
                ScrollDown(Index);
                return true;
            case "space":
                Space();
                return true;
            case "number":
                Number(input ?? string.Empty);
                return true;
            case "reset":
                Reset();
                return true;
            case "newItem":
                NewItem();
                return true;
            case "a" when Multiple:
                A();
                return true;
            case "i" when Multiple:
                I();
                return true;
            case "g" when Multiple:
                G();
                return true;
            default:
                return false;
        }
    }

    protected override void Dispatch(string? input, KeyEvent key)
    {
        // Select.dispatch: only multiselects look methods up by key name
        if (Multiple && key.Name is { } name && Method(name, input, key))
        {
            return;
        }

        Alert();
    }

    public PromptChoice? Focus(PromptChoice choice, bool? enabled = null)
    {
        bool value = enabled ?? choice.Enabled == true;
        if (value && choice.Enabled != true && Selected.Count >= MaxSelected)
        {
            Alert();

            return null;
        }

        Index = choice.Index;
        choice.Enabled = value && !IsDisabled(choice);

        return choice;
    }

    public void Space()
    {
        if (!Multiple)
        {
            Alert();

            return;
        }

        if (Focused is null)
        {
            return;
        }

        Toggle(Focused);
        Render();
    }

    public void A()
    {
        if (MaxSelected < Choices.Count)
        {
            Alert();

            return;
        }

        bool enabled = Selectable.All(choice => choice.Enabled == true);
        foreach (PromptChoice choice in Choices)
        {
            choice.Enabled = !enabled;
        }

        Render();
    }

    public void I()
    {
        if (Choices.Count - Selected.Count > MaxSelected)
        {
            Alert();

            return;
        }

        foreach (PromptChoice choice in Choices)
        {
            choice.Enabled = choice.Enabled != true;
        }

        Render();
    }

    public void G()
    {
        // no nested choices: `g` behaves as `a`
        A();
    }

    public virtual PromptChoice? Toggle(PromptChoice choice, bool? enabled = null)
    {
        if (choice.Enabled != true && Selected.Count >= MaxSelected)
        {
            Alert();

            return null;
        }

        choice.Enabled = enabled ?? choice.Enabled != true;

        return choice;
    }

    public void Enable(PromptChoice choice)
    {
        if (Selected.Count >= MaxSelected)
        {
            Alert();

            return;
        }

        choice.Enabled = !IsDisabled(choice);
    }

    /// <summary>`number(n)`: jump to the choice with that index (enquirer waits `delay` ms for a second digit; with 0 ms none arrives).</summary>
    private string _num = string.Empty;

    public void Number(string digit)
    {
        _num += digit;
        string num = _num;
        int len = Choices.Count;

        if (num == "0" || (num.Length == 1 && int.Parse(num + "0") > len))
        {
            HandleNumber(num);

            return;
        }

        if (int.Parse(num) > len)
        {
            _num = string.Empty;
            Alert();

            return;
        }

        HandleNumber(num);
    }

    private void HandleNumber(string num)
    {
        _num = string.Empty;
        int i = int.Parse(num);
        if (i > Choices.Count - 1)
        {
            Alert();

            return;
        }

        PromptChoice focused = Focused!;
        PromptChoice choice = Choices.First(item => item.Index == i);

        if (choice.Enabled != true && Selected.Count >= MaxSelected)
        {
            Alert();

            return;
        }

        if (!Visible.Contains(choice))
        {
            List<PromptChoice> choices = Reorder(Choices);
            int actualIndex = choices.IndexOf(choice);

            if (focused.Index > actualIndex)
            {
                List<PromptChoice> start = [.. choices.Skip(actualIndex).Take(Limit)];
                List<PromptChoice> end = [.. choices.Where(item => !start.Contains(item))];
                Choices = [.. start, .. end];
            }
            else
            {
                int pos = actualIndex - Limit + 1;
                Choices = [.. choices.Skip(pos), .. choices.Take(pos)];
            }
        }

        Index = Choices.IndexOf(choice);
        Toggle(Focused!);
        Render();
    }

    private static List<PromptChoice> Reorder(IEnumerable<PromptChoice> choices)
    {
        return [.. choices.OrderBy(choice => choice.Index)];
    }

    public void Home()
    {
        Choices = Reorder(Choices);
        Index = 0;
        Render();
    }

    public void End()
    {
        int pos = Choices.Count - Limit;
        List<PromptChoice> choices = Reorder(Choices);
        Choices = [.. choices.Skip(pos), .. choices.Take(pos)];
        Index = Limit - 1;
        Render();
    }

    public void First()
    {
        Index = 0;
        Render();
    }

    public void Last()
    {
        Index = Visible.Count - 1;
        Render();
    }

    public void Prev()
    {
        if (Visible.Count <= 1)
        {
            Alert();

            return;
        }

        Up();
    }

    public void Next()
    {
        if (Visible.Count <= 1)
        {
            Alert();

            return;
        }

        Down();
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

    public void Up()
    {
        int len = Choices.Count;
        int vis = Visible.Count;
        int idx = Index;
        if (len > vis && idx == 0)
        {
            ScrollUp();

            return;
        }

        // JavaScript precedence: idx - (1 % len)
        Index = (((idx - (1 % len)) + len) % len);
        if (IsDisabled(Focused) && !AllChoicesAreDisabled())
        {
            Up();

            return;
        }

        Render();
    }

    public void Down()
    {
        int len = Choices.Count;
        int vis = Visible.Count;
        int idx = Index;
        if (len > vis && idx == vis - 1)
        {
            ScrollDown();

            return;
        }

        Index = (idx + 1) % len;
        if (IsDisabled(Focused) && !AllChoicesAreDisabled())
        {
            Down();

            return;
        }

        Render();
    }

    public void ScrollUp(int i = 0)
    {
        List<PromptChoice> choices = [.. Choices];
        PromptChoice last = choices[^1];
        choices.RemoveAt(choices.Count - 1);
        Choices = [last, .. choices];
        Index = i;
        if (IsDisabled(Focused))
        {
            Up();

            return;
        }

        Render();
    }

    public void ScrollDown(int? i = null)
    {
        int target = i ?? Visible.Count - 1;
        List<PromptChoice> choices = Choices;
        Choices = [.. choices.Skip(1), choices[0]];
        Index = target;
        if (IsDisabled(Focused))
        {
            Down();

            return;
        }

        Render();
    }

    public void PageUp()
    {
        if (Visible.Count <= 1)
        {
            Alert();

            return;
        }

        Limit = Math.Max(Limit - 1, 0);
        Index = Math.Min(Limit - 1, Index);
        _limit = Limit;
        if (IsDisabled(Focused))
        {
            Up();

            return;
        }

        Render();
    }

    public void PageDown()
    {
        if (Visible.Count >= Choices.Count)
        {
            Alert();

            return;
        }

        Index = Math.Max(0, Index);
        Limit = Math.Min(Limit + 1, Choices.Count);
        _limit = Limit;
        if (IsDisabled(Focused))
        {
            Down();

            return;
        }

        Render();
    }

    /// <summary>`newItem()` (Ctrl+N): adds an editable choice that nothing can type into, so it can only be dismissed with a cancel.</summary>
    public void NewItem()
    {
        var choice = new PromptChoice { Name = "New choice name?", Message = "New choice name?", NewChoice = true, Index = Choices.Count };
        choice.Value = choice.Name;
        choice.Enabled = false;
        Choices.Add(choice);
        Index = Choices.Count - 1;
        Limit = Choices.Count;
        Render();
    }

    // ---- submit ------------------------------------------------------------------------------------------------------

    public override void Submit()
    {
        PromptChoice? choice = Focused;
        if (choice is null)
        {
            Alert();

            return;
        }

        if (choice.NewChoice)
        {
            if (choice.Input.Length == 0)
            {
                Alert();

                return;
            }

            choice.NewChoice = false;
            choice.Name = choice.Message = choice.Input;
            choice.Input = string.Empty;
            choice.Cursor = 0;
            Render();

            return;
        }

        if (Choices.Any(item => item.NewChoice))
        {
            Alert();

            return;
        }

        List<PromptChoice> value = Selected;
        if (Multiple)
        {
            value = Reorder(value);
            Value = value.Select(item => item.Name).ToList();
        }
        else
        {
            Value = value[0].Name;
        }

        base.Submit();
    }

    /// <summary>`ArrayPrompt.value`: an unset value falls back to the (empty) input rather than the initial.</summary>
    public override object? Value
    {
        get
        {
            object? value = base.Value;
            bool isInitial = value is int a ? Initial is int b && a == b : ReferenceEquals(value, Initial);

            return value is not string && isInitial ? Input : value;
        }
        set => base.Value = value;
    }

    // ---- rendering ---------------------------------------------------------------------------------------------------

    protected override string Format()
    {
        if (!State.Submitted || State.Cancelled)
        {
            return string.Empty;
        }

        if (Multiple)
        {
            return string.Join(", ", Selected.Select(choice => Palette.Primary.Apply(choice.Name)));
        }

        return Palette.Primary.Apply(Selected[0].Name);
    }

    public override string Separator()
    {
        string separator = Palette.Muted.Apply(Symbols.Ellipsis);

        return State.Submitted ? base.Separator() : separator;
    }

    protected virtual string Pointer(PromptChoice choice, int i)
    {
        if (Multiple && PointerOption is null)
        {
            return string.Empty;
        }

        // Prompt.pointer
        string? value = PointerOption ?? Symbols.Pointer;
        if (Style.HasColor(value))
        {
            return value;
        }

        if (value.Length > 0)
        {
            bool focused = Index == i;
            string styled = focused ? Palette.Primary.Apply(value) : value;

            return focused ? styled : new string(' ', value.Length);
        }

        return string.Empty;
    }

    protected virtual string Indicator(PromptChoice choice, int i)
    {
        if (!Multiple)
        {
            return string.Empty;
        }

        // Prompt.indicator
        string value = choice.Indicator ?? Symbols.Indicator;
        if (Style.HasColor(value))
        {
            return value;
        }

        bool enabled = choice.Enabled == true;
        Style style = enabled ? Palette.Success : Palette.Dark;

        return style.Apply(value);
    }

    protected virtual string ChoiceMessage(PromptChoice choice, int i)
    {
        string message = choice.Message;
        if (choice.Role == "heading" && !Style.HasColor(message))
        {
            message = Palette.Strong.Apply(message);
        }

        return message;
    }

    protected virtual string RenderChoice(PromptChoice choice, int i)
    {
        bool focused = Index == i;
        string pointer = Pointer(choice, i);
        string check = Indicator(choice, i) + choice.Pad;
        string? hint = choice.Hint;
        if (!string.IsNullOrEmpty(hint) && !Style.HasColor(hint))
        {
            hint = Palette.Muted.Apply(hint);
        }

        string indent = choice.Indent;
        string message = ChoiceMessage(choice, i);

        string Line()
        {
            return string.Join(" ", new[] { string.Empty, indent + pointer + check, message, string.Empty, hint ?? string.Empty }
                .Where(part => part.Length > 0));
        }

        if (choice.Role == "heading")
        {
            return Line();
        }

        if (choice.Disabled)
        {
            if (!Style.HasColor(message))
            {
                message = Palette.Disabled.Apply(message);
            }

            return Line();
        }

        if (focused)
        {
            message = Palette.Em.Apply(message);
        }

        return Line();
    }

    protected virtual string RenderChoices()
    {
        if (State.Submitted)
        {
            return string.Empty;
        }

        List<string> visible = [.. Visible.Select((choice, i) => RenderChoice(choice, i))];
        if (visible.Count == 0)
        {
            visible.Add(Palette.Danger.Apply("No matching choices"));
        }

        return string.Join("\n", visible);
    }

    public override void Render()
    {
        int size = State.Size;
        string prompt = string.Empty;
        string header = string.Empty;
        string prefix = Prefix();
        string separator = Separator();
        string message = Message();
        prompt = string.Join(" ", prefix, message, separator, string.Empty);
        State.Prompt = prompt;

        string output = Format();
        string help = ErrorText().Length > 0 ? ErrorText() : Hint();
        string body = RenderChoices();
        string footer = string.Empty;

        if (output.Length > 0)
        {
            prompt += output;
        }

        if (help.Length > 0 && !prompt.Contains(help, StringComparison.Ordinal))
        {
            prompt += " " + help;
        }

        if (State.Submitted && output.Length == 0 && body.Trim().Length == 0 && Multiple)
        {
            prompt += Palette.Danger.Apply(EmptyError);
        }

        Clear(size);
        Write(string.Join("\n", new[] { header, prompt, body, footer }.Where(part => part.Length > 0)));
        Restore();
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/checklist.js - a multiselect with a trailing action choice.</remarks>
internal sealed class Checklist : SelectPrompt
{
    public Checklist(PromptOptions options)
        : base(options, true, "multiselect")
    {
    }

    protected override string? PointerOption => "❯";

    protected override IEnumerable<ChoiceSpec> ChoiceSpecs => [.. Options.Choices, new ChoiceSpec("__submit", Options.SubmitLabel ?? string.Empty)];

    protected override PromptChoice ToChoice(ChoiceSpec spec, int index)
    {
        PromptChoice choice = base.ToChoice(spec, index);
        if (spec.Name == "__submit")
        {
            choice.Action = true;
        }

        return choice;
    }

    protected override string Indicator(PromptChoice choice, int i)
    {
        if (choice.Action)
        {
            return " ";
        }

        return choice.Enabled == true ? "◉" : Palette.Muted.Apply("○");
    }

    protected override string RenderChoice(PromptChoice choice, int index)
    {
        if (choice.Action && Options.SubmitLabelFunction is { } label)
        {
            choice.Message = label([.. Selected.Select(item => item.Name)]);
        }

        string line = base.RenderChoice(choice, index);

        return choice.Action ? "\n" + line : line;
    }

    public override PromptChoice? Toggle(PromptChoice choice, bool? enabled = null)
    {
        return choice.Action ? choice : base.Toggle(choice, enabled);
    }

    public override void Submit()
    {
        if (Focused is null || !Focused.Action)
        {
            Space();

            return;
        }

        base.Submit();
    }

    public override List<PromptChoice> Selected => [.. base.Selected.Where(choice => !choice.Action)];

    public override void Render()
    {
        if (State.Submitted && !State.Cancelled)
        {
            Clear(State.Size);
            State.Prompt = string.Empty;
            State.Size = 0;

            return;
        }

        base.Render();
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/custodySelect.js - Escape cancels, Left jumps to the back choice.</remarks>
internal sealed class CustodySelect : SelectPrompt
{
    public CustodySelect(PromptOptions options)
        : base(options)
    {
    }

    public override void Keypress(string? input, KeyEvent? evt)
    {
        KeyEvent key = evt ?? new KeyEvent();
        if (key.Name == "escape")
        {
            Cancel();

            return;
        }

        if (key.Name == "left" && Options.BackValue is { } backValue)
        {
            Index = Choices.FindIndex(choice => choice.Name == backValue);
            Submit();

            return;
        }

        base.Keypress(input, evt);
    }

    public override void Render()
    {
        // Replace the previous step instead of leaving a trail of completed menus.
        if (State.Submitted)
        {
            Clear(State.Size);
            State.Prompt = string.Empty;
            State.Size = 0;

            return;
        }

        base.Render();
    }
}
