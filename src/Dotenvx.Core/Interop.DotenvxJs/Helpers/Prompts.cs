using Dotenvx.Interop.EnquirerJs;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>Raised when the user cancels a prompt (Ctrl+C); upstream's `PROMPT_CANCELLED` error.</summary>
internal class PromptCancelledException() : Exception("prompt cancelled");

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/prompts.js. Upstream wraps enquirer; the wrapping here is the same, over a C# port of
/// enquirer's prompts (Interop.EnquirerJs), so the terminal sees the same bytes. Stdin ending before a prompt is answered leaves
/// upstream's promise pending, which makes Node exit 0 quietly - <see cref="EndOfInput" />.
/// </remarks>
internal static class Prompts
{
    /// <summary>A choice for <see cref="Select" />: the text shown and the value returned.</summary>
    public record Choice(string Name, string Value, bool Disabled = false);

    /// <summary>The options of prompts.js `select`.</summary>
    public sealed record SelectRequest(string Message, IReadOnlyList<Choice> Choices)
    {
        public bool Navigation { get; init; }

        public string? BackValue { get; init; }

        public string? Initial { get; init; }

        public string? Prefix { get; init; }

        public string? Footer { get; init; }
    }

    /// <summary>The options of prompts.js `multiselect`.</summary>
    public sealed record MultiselectRequest(string Message, IReadOnlyList<Choice> Choices)
    {
        public IReadOnlyList<string> Initial { get; init; } = [];

        public string? SubmitLabel { get; init; }

        public Func<IReadOnlyList<string>, string>? SubmitLabelFunction { get; init; }
    }

    /// <summary>Where prompts render and read (swappable so tests can drive them with scripted keys).</summary>
    public static IPromptOutput PromptOutput { get; set; } = ConsolePromptOutput.Instance;

    public static IKeySource KeySource { get; set; } = ConsoleKeySource.Instance;

    private static readonly Func<SelectRequest, string?> DefaultSelect = RunSelect;

    private static readonly Func<MultiselectRequest, IReadOnlyList<string>?> DefaultMultiselect = RunMultiselect;

    private static readonly Func<string, string?> DefaultInput = message => RunInput(message, null);

    private static readonly Func<string, string?, string?> DefaultInputWithInitial = RunInput;

    private static readonly Func<string, bool, bool?> DefaultConfirm = RunConfirm;

    private static readonly Func<string, string, string, string?> DefaultPassword = RunPassword;

    /// <summary>The chosen value, or null when input ended before an answer. Throws <see cref="PromptCancelledException" />.</summary>
    public static Func<SelectRequest, string?> Select { get; internal set; } = DefaultSelect;

    public static Func<MultiselectRequest, IReadOnlyList<string>?> Multiselect { get; internal set; } = DefaultMultiselect;

    public static Func<string, string?> InputText { get; internal set; } = DefaultInput;

    /// <summary>prompts.js `input({ message, initial })`: (message, initial) => the entered text.</summary>
    public static Func<string, string?, string?> InputWithInitial { get; internal set; } = DefaultInputWithInitial;

    public static Func<string, bool, bool?> Confirm { get; internal set; } = DefaultConfirm;

    /// <summary>(message, prefix, separator) => the entered text. Throws <see cref="PromptCancelledException" />.</summary>
    public static Func<string, string, string, string?> Password { get; internal set; } = DefaultPassword;

    /// <summary>Convenience for the callers that ask for a plain selection (no navigation, no prefix).</summary>
    public static string? SelectOne(string message, IReadOnlyList<Choice> choices)
    {
        return Select(new SelectRequest(message, choices));
    }

    public static void ResetForTests() => ResetToDefaults();

    internal static void ResetToDefaults()
    {
        PromptOutput = ConsolePromptOutput.Instance;
        KeySource = ConsoleKeySource.Instance;
        InputEnded = () => Environment.Exit(0);
        Select = DefaultSelect;
        Multiselect = DefaultMultiselect;
        InputText = DefaultInput;
        InputWithInitial = DefaultInputWithInitial;
        Confirm = DefaultConfirm;
        Password = DefaultPassword;
    }

    /// <summary>What happens when stdin ends before a prompt is answered: the process exits silently with 0, as Node does.</summary>
    public static Action InputEnded { get; set; } = () => Environment.Exit(0);

    private static T EndOfInput<T>()
    {
        InputEnded();

        return default!;
    }

    private static PromptOptions Options(string message)
    {
        return new PromptOptions { Message = message, Output = PromptOutput, Input = KeySource };
    }

    /// <summary>prompts.js `choicesForSelect`.</summary>
    private static ChoiceSpec[] ChoicesForSelect(IReadOnlyList<Choice> choices)
    {
        return [.. choices.Select(choice => new ChoiceSpec(choice.Value, choice.Name.Length > 0 ? choice.Name : choice.Value,
            Disabled: choice.Disabled ? true : null, DisabledHint: choice.Disabled ? "(unavailable)" : null))];
    }

    private static string? RunSelect(SelectRequest request)
    {
        PromptOptions options = Options(request.Message);
        options.Choices = ChoicesForSelect(request.Choices);
        options.Footer = request.Footer;
        if (request.Prefix is { Length: > 0 } prefix)
        {
            options.PrefixPending = prefix;
        }

        try
        {
            if (request.Navigation)
            {
                options.InitialName = request.Initial;
                options.BackValue = request.BackValue;

                return (string?)new CustodySelect(options).Run();
            }

            return (string?)new SelectPrompt(options).Run();
        }
        catch (PromptInputEndedException)
        {
            return EndOfInput<string?>();
        }
        catch (PromptCancelled)
        {
            throw new PromptCancelledException();
        }
    }

    private static IReadOnlyList<string>? RunMultiselect(MultiselectRequest request)
    {
        PromptOptions options = Options(request.Message);
        options.Choices = ChoicesForSelect(request.Choices);
        options.InitialNames = request.Initial;
        try
        {
            if (request.SubmitLabel is not null || request.SubmitLabelFunction is not null)
            {
                options.SubmitLabel = request.SubmitLabel;
                options.SubmitLabelFunction = request.SubmitLabelFunction;

                return (IReadOnlyList<string>?)new Checklist(options).Run();
            }

            return (IReadOnlyList<string>?)new SelectPrompt(options, true, "multiselect").Run();
        }
        catch (PromptInputEndedException)
        {
            return EndOfInput<IReadOnlyList<string>?>();
        }
        catch
        {
            throw new PromptCancelledException();
        }
    }

    private static string? RunInput(string message, string? initial)
    {
        try
        {
            PromptOptions options = Options(message);
            options.PresetInput = initial;

            return (string?)new InputPrompt(options).Run();
        }
        catch (PromptInputEndedException)
        {
            return EndOfInput<string?>();
        }
        catch
        {
            throw new PromptCancelledException();
        }
    }

    private static bool? RunConfirm(string message, bool initial)
    {
        PromptOptions options = Options(message);
        options.InitialBool = initial;
        try
        {
            return new ConfirmPrompt(options).Run() is true;
        }
        catch (PromptInputEndedException)
        {
            return EndOfInput<bool?>();
        }
        catch
        {
            throw new PromptCancelledException();
        }
    }

    /// <summary>prompts.js `clearLastLine`: up one line, erase it.</summary>
    private static void ClearLastLine()
    {
        PromptOutput.Write("\u001b[1A\u001b[2K");
    }

    private static string? RunPassword(string message, string prefix, string separator)
    {
        PromptOptions options = Options(message);
        options.PrefixPending = options.PrefixSubmitted = options.PrefixCancelled = prefix;
        options.SeparatorPending = options.SeparatorSubmitted = options.SeparatorCancelled = separator;
        try
        {
            string? answer = (string?)new PasswordPrompt(options).Run();
            ClearLastLine();

            return answer;
        }
        catch (PromptInputEndedException)
        {
            return EndOfInput<string?>();
        }
        catch
        {
            ClearLastLine();

            throw new PromptCancelledException();
        }
    }
}
