using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.EnquirerJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/prompts.js. The byte-for-byte comparison with enquirer lives in
/// Dotenvx.ParityTest (EnquirerParityTest, PtyParityTest); these tests pin the behaviour of the seams the actions use.
/// </remarks>
public class PromptsTest : IDisposable
{
    private const string Down = "\u001b[B";
    private const string Up = "\u001b[A";
    private const string Enter = "\r";
    private const string CtrlC = "\u0003";

    private readonly Output _output = new();

    private static readonly Prompts.Choice[] Choices =
    [
        new("one", "1"),
        new("two", "2"),
        new("three", "3", true)
    ];

    public PromptsTest()
    {
        Prompts.ResetForTests();
        Prompts.PromptOutput = _output;
    }

    public void Dispose()
    {
        Prompts.ResetForTests();
    }

    private void Keys(params string[] chunks)
    {
        Prompts.KeySource = new ScriptedKeys(chunks);
    }

    [Fact]
    public void TestSelectReturnsTheFocusedChoiceValue()
    {
        Keys(Down, Enter);

        Prompts.SelectOne("pick", Choices).ShouldBe("2");
    }

    [Fact]
    public void TestSelectSkipsDisabledChoices()
    {
        Keys(Down, Down, Enter);

        // wraps past the disabled third choice back to the first
        Prompts.SelectOne("pick", Choices).ShouldBe("1");
    }

    [Fact]
    public void TestSelectWrapsUpwards()
    {
        Keys(Up, Enter);

        Prompts.SelectOne("pick", Choices).ShouldBe("2");
    }

    [Fact]
    public void TestSelectWritesToTheRenderStreamOnly()
    {
        Keys(Enter);

        Prompts.SelectOne("pick", Choices);

        _output.Text.ShouldContain("pick");
        _output.Text.ShouldContain("one");
    }

    [Fact]
    public void TestCtrlCCancels()
    {
        Keys(CtrlC);

        Should.Throw<PromptCancelledException>(() => Prompts.SelectOne("pick", Choices));
    }

    [Fact]
    public void TestInitialChoiceStartsFocused()
    {
        Keys(Enter);

        Prompts.Select(new Prompts.SelectRequest("pick", Choices) { Navigation = true, Initial = "2" }).ShouldBe("2");
    }

    [Fact]
    public void TestMultiselectToggleWithSpaceAndSubmit()
    {
        Keys(" ", Down, " ", Enter);

        Prompts.Multiselect(new Prompts.MultiselectRequest("pick", Choices)).ShouldBe(["1", "2"]);
    }

    [Fact]
    public void TestMultiselectStartsWithTheInitialSelection()
    {
        Keys(Enter);

        Prompts.Multiselect(new Prompts.MultiselectRequest("pick", Choices) { Initial = ["2"] }).ShouldBe(["2"]);
    }

    [Fact]
    public void TestInputReturnsTheTypedText()
    {
        Keys("h", "i", Enter);

        Prompts.InputText("say").ShouldBe("hi");
    }

    [Fact]
    public void TestPasswordIsNeverEchoed()
    {
        Keys("s", "e", "c", Enter);

        Prompts.Password("enter it", "?", ":").ShouldBe("sec");

        _output.Text.ShouldNotContain("sec");
        _output.Text.ShouldContain("***");
    }

    [Fact]
    public void TestConfirmAnswersWithAKey()
    {
        Keys("y");

        Prompts.Confirm("sure", false).ShouldBe(true);
    }

    [Fact]
    public void TestEndOfInputCallsTheExitHook()
    {
        int exits = 0;
        Prompts.InputEnded = () =>
        {
            exits++;
            throw new PromptInputEndedException();
        };
        Keys();

        Should.Throw<PromptInputEndedException>(() => Prompts.SelectOne("pick", Choices));
        exits.ShouldBe(1);
    }

    private sealed class ScriptedKeys(string[] chunks) : IKeySource
    {
        private int _next;

        public string? ReadChunk() => _next < chunks.Length ? chunks[_next++] : null;
    }

    private sealed class Output : IPromptOutput
    {
        private readonly System.Text.StringBuilder _text = new();

        public string Text => _text.ToString();

        public int? Columns => 80;

        public int? Rows => 24;

        public void Write(string text) => _text.Append(text);
    }
}
