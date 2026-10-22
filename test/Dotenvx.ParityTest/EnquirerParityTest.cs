using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.EnquirerJs;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - the C# port of enquirer (Interop.EnquirerJs) against the real thing: dotenvx's own prompts.js, running in
/// Node, and the ported prompts get the same keys in a single chunk of piped input; everything written to the render stream
/// (every colour code, cursor move and clear) plus the result must be identical.
/// </remarks>
[Collection(NodeParityCollection.Name)]
public class EnquirerParityTest(NodeParityFixture fixture)
{
    private static readonly (string Name, string Value, bool Disabled)[] Three =
        [("Choice a", "a", false), ("Choice b", "b", false), ("Choice c", "c", false)];

    private sealed class EndedException : Exception;

    private sealed class FakeOutput(int? columns, int? rows) : IPromptOutput
    {
        public StringBuilder Text { get; } = new();

        public int? Columns => columns;

        public int? Rows => rows;

        public void Write(string text) => Text.Append(text);
    }

    private sealed class FakeKeys(string chunk) : IKeySource
    {
        private bool _done;

        public string? ReadChunk()
        {
            if (_done)
            {
                return null;
            }

            _done = true;

            return chunk;
        }
    }

    /// <summary>`&lt;down&gt;`-style key names to the bytes a terminal sends.</summary>
    private static string Keys(string text)
    {
        var names = new Dictionary<string, string>
        {
            ["up"] = "\u001b[A",
            ["down"] = "\u001b[B",
            ["right"] = "\u001b[C",
            ["left"] = "\u001b[D",
            ["home"] = "\u001b[H",
            ["end"] = "\u001b[F",
            ["pgup"] = "\u001b[5~",
            ["pgdn"] = "\u001b[6~",
            ["del"] = "\u001b[3~",
            ["esc"] = "\u001b",
            ["enter"] = "\r",
            ["lf"] = "\n",
            ["tab"] = "\t",
            ["s-tab"] = "\u001b[Z",
            ["bs"] = "\u007f",
            ["space"] = " ",
            ["ctrl-c"] = "\u0003",
            ["ctrl-a"] = "\u0001",
            ["ctrl-e"] = "\u0005",
            ["ctrl-b"] = "\u0002",
            ["ctrl-f"] = "\u0006",
            ["ctrl-d"] = "\u0004",
            ["ctrl-k"] = "\u000b",
            ["ctrl-w"] = "\u0017",
            ["ctrl-x"] = "\u0018",
            ["ctrl-v"] = "\u0016",
            ["ctrl-g"] = "\u0007",
            ["ctrl-n"] = "\u000e",
            ["ctrl-u"] = "\u0015",
            ["alt-b"] = "\u001bb",
            ["alt-left"] = "\u001b[1;3D",
            ["s-up"] = "\u001b[1;2A",
            ["s-down"] = "\u001b[1;2B",
            ["c-up"] = "\u001b[1;5A",
            ["f1"] = "\u001bOP",
            ["ins"] = "\u001b[2~"
        };

        return System.Text.RegularExpressions.Regex.Replace(text, "<([a-z0-9-]+)>", match => names[match.Groups[1].Value]);
    }

    public sealed record Scenario(string Name, string Kind, string Keys)
    {
        public string Message { get; init; } = "Pick one";

        public (string Name, string Value, bool Disabled)[] Choices { get; init; } = Three;

        public string? Prefix { get; init; }

        public string? Separator { get; init; }

        public string? Initial { get; init; }

        public string[]? InitialNames { get; init; }

        public string? BackValue { get; init; }

        public string? SubmitLabel { get; init; }

        public string? LabelMode { get; init; }

        public bool Configured { get; init; }

        public bool CurrentFilter { get; init; }

        public bool CurrentIgnore { get; init; }

        public bool InitialBool { get; init; }

        public int? Columns { get; init; }

        public int? Rows { get; init; }

        public override string ToString() => Name;
    }

    private static (string Name, string Value, bool Disabled)[] Many(int count)
    {
        return [.. Enumerable.Range(0, count).Select(i => ($"Choice {i}", $"v{i}", false))];
    }

    private static readonly (string Name, string Value, bool Disabled)[] WithDisabled =
        [("Choice a", "a", false), ("Choice b (off)", "b", true), ("Choice c", "c", false), ("Choice d", "d", true)];

    private static readonly (string Name, string Value, bool Disabled)[] Protect =
    [
        ("Protect plaintext secrets from code commits (.env*)", "filter", false),
        ("Protect private keys from code commits (.env.keys*)", "ignore", false)
    ];

    public static IEnumerable<object[]> Scenarios()
    {
        var list = new List<Scenario>
        {
            // select
            new("select enter", "select", "<enter>"),
            new("select lf", "select", "<lf>"),
            new("select down enter", "select", "<down><enter>"),
            new("select up wraps", "select", "<up><enter>"),
            new("select down x3 wraps", "select", "<down><down><down><enter>"),
            new("select digit", "select", "2<enter>"),
            new("select digit out of range", "select", "9<enter>"),
            new("select letter alerts", "select", "x<enter>"),
            new("select space alerts", "select", "<space><enter>"),
            new("select left right alert", "select", "<left><right><enter>"),
            new("select tab shift-tab", "select", "<tab><s-tab><s-tab><enter>"),
            new("select home end", "select", "<end><home><enter>"),
            new("select pgup pgdn", "select", "<pgdn><pgup><enter>"),
            new("select first last", "select", "<ctrl-e><ctrl-a><enter>"),
            new("select ctrl-g reset", "select", "<down><ctrl-g><enter>"),
            new("select ctrl-n", "select", "<ctrl-n><enter>"),
            new("select ctrl-n then down", "select", "<ctrl-n><down><enter>"),
            new("select ctrl-n then up", "select", "<ctrl-n><up><enter>"),
            new("select ctrl-n twice", "select", "<ctrl-n><ctrl-n><enter>"),
            new("select ctrl-n then digit", "select", "<ctrl-n>1<enter>"),
            new("select ctrl-n then typing", "select", "<ctrl-n>abc<enter>"),
            new("select ctrl-n then home end", "select", "<ctrl-n><home><end><enter>"),
            new("select ctrl-n then ctrl-g", "select", "<ctrl-n><ctrl-g><enter>"),
            new("multiselect ctrl-n then space", "multiselect", "<ctrl-n><space><enter>"),
            new("multiselect ctrl-n then down space", "multiselect", "<ctrl-n><down><space><enter>"),
            new("multiselect ctrl-n then up space", "multiselect", "<ctrl-n><up><space><enter>"),
            new("multiselect ctrl-n then a", "multiselect", "<ctrl-n>a<enter>"),
            new("multiselect ctrl-n then i", "multiselect", "<ctrl-n>i<enter>"),
            new("multiselect ctrl-n then g", "multiselect", "<ctrl-n>g<enter>"),
            new("multiselect ctrl-n then typing", "multiselect", "<ctrl-n>xyz<enter>"),
            new("multiselect space ctrl-n enter", "multiselect", "<space><ctrl-n><enter>"),
            new("multiselect ctrl-n then pgdn", "multiselect", "<ctrl-n><pgdn><pgup><space><enter>"),
            new("checklist ctrl-n", "checklist", "<ctrl-n><enter><space><enter>") { Choices = Protect, InitialNames = ["filter", "ignore"], LabelMode = "protect", Message = "Set protections" },
            new("custody ctrl-n", "custody", "<ctrl-n><enter>"),
            new("custody ctrl-n then down", "custody", "<ctrl-n><down><enter>"),
            new("select ctrl-c", "select", "<ctrl-c>"),
            new("select escape", "select", "<esc>"),
            new("select unknown escape sequence", "select", "<f1><ins><del><enter>"),
            new("select modified arrows", "select", "<s-down><c-up><enter>"),
            new("select no enter", "select", "<down>"),
            new("select empty input", "select", string.Empty),
            new("select keys after enter are dropped", "select", "<enter><down><down>"),
            new("select prefix", "select", "<down><enter>") { Prefix = "⛨" },
            new("select unicode message", "select", "<enter>") { Message = "Select équipe ⛨" },
            new("select disabled", "select", "<down><enter>") { Choices = WithDisabled },
            new("select disabled first", "select", "<enter>") { Choices = [("Off", "o", true), ("Choice a", "a", false), ("Choice b", "b", false)] },
            new("select disabled wrap", "select", "<down><down><down><up><enter>") { Choices = WithDisabled },
            new("select one choice", "select", "<down><enter>") { Choices = [("Only", "only", false)] },
            new("select message with colour", "select", "<enter>") { Message = "\u001b[31mred\u001b[39m" },
            new("select name equals value", "select", "<enter>") { Choices = [("a", "a", false), ("b", "b", false)] },
            new("select scroll 30 rows 10 down", "select", "<down><down><down><down><down><down><down><down><down><down><down><enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select scroll up from first", "select", "<up><up><enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select scroll end home", "select", "<end><home><enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select scroll digit", "select", "2<enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select scroll digit two", "select", "25<enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select scroll pgdn", "select", "<pgdn><pgdn><pgup><enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select scroll shift", "select", "<s-down><s-down><s-up><enter>") { Choices = Many(30), Rows = 10, Columns = 80 },
            new("select narrow columns", "select", "<down><enter>") { Columns = 12 },
            new("select wide message wraps", "select", "<enter>") { Message = "A very long message that does not fit in twenty columns", Columns = 20 },
            new("select many default rows", "select", "<down><down><enter>") { Choices = Many(40) },

            // custody select
            new("custody enter", "custody", "<enter>") { Choices = [("Local", "local", false), ("Armor", "armored", false), ("Passwords", "passwords", false)] },
            new("custody down enter", "custody", "<down><enter>"),
            new("custody escape", "custody", "<esc>"),
            new("custody ctrl-c", "custody", "<ctrl-c>"),
            new("custody left with back", "custody", "<left>") { Choices = [("A", "a", false), ("← back", "__back", false)], BackValue = "__back" },
            new("custody left without back value", "custody", "<left><enter>"),
            new("custody initial", "custody", "<enter>") { Initial = "b" },
            new("custody initial missing", "custody", "<enter>") { Initial = "nope" },
            new("custody prefix and back", "custody", "<down><enter>") { Prefix = "⛉", Choices = [("A", "a", false), ("← back", "__back", false)], BackValue = "__back" },

            // multiselect
            new("multiselect enter", "multiselect", "<enter>"),
            new("multiselect space enter", "multiselect", "<space><enter>"),
            new("multiselect toggle two", "multiselect", "<space><down><down><space><enter>"),
            new("multiselect initial", "multiselect", "<enter>") { InitialNames = ["a", "c"] },
            new("multiselect initial toggle off", "multiselect", "<space><enter>") { InitialNames = ["a"] },
            new("multiselect a all", "multiselect", "a<enter>"),
            new("multiselect a twice", "multiselect", "aa<enter>"),
            new("multiselect i invert", "multiselect", "i<enter>") { InitialNames = ["a"] },
            new("multiselect g", "multiselect", "g<enter>"),
            new("multiselect digit toggles", "multiselect", "2<enter>"),
            new("multiselect ctrl-c", "multiselect", "<space><ctrl-c>"),
            new("multiselect escape", "multiselect", "<esc>"),
            new("multiselect letter alerts", "multiselect", "z<enter>"),
            new("multiselect disabled", "multiselect", "<space><down><space><enter>") { Choices = WithDisabled, InitialNames = ["a"] },
            new("multiselect scroll", "multiselect", "<down><down><down><down><space><enter>") { Choices = Many(20), Rows = 6, Columns = 80 },

            // checklist (protect / spec init)
            new("checklist initial both", "checklist", "<down><down><enter>") { Choices = Protect, InitialNames = ["filter", "ignore"], LabelMode = "protect", Message = "Set protections" },
            new("checklist toggle first then apply", "checklist", "<enter><down><down><enter>") { Choices = Protect, InitialNames = ["filter", "ignore"], LabelMode = "protect", Message = "Set protections" },
            new("checklist toggle all off", "checklist", "<enter><down><enter><down><enter>") { Choices = Protect, InitialNames = ["filter", "ignore"], LabelMode = "protect", Message = "Set protections" },
            new("checklist configured same", "checklist", "<down><down><enter>") { Choices = Protect, InitialNames = ["filter", "ignore"], LabelMode = "protect", Message = "Set protections", Configured = true, CurrentFilter = true, CurrentIgnore = true },
            new("checklist configured changed", "checklist", "<space><down><down><enter>") { Choices = Protect, InitialNames = ["filter", "ignore"], LabelMode = "protect", Message = "Set protections", Configured = true, CurrentFilter = true, CurrentIgnore = true },
            new("checklist none initial submit", "checklist", "<down><down><enter>") { Choices = Protect, LabelMode = "protect", Message = "Set protections" },
            new("checklist fixed label", "checklist", "<down><down><enter>") { Choices = Three, InitialNames = ["a"], SubmitLabel = "Create Envfile", Message = "Create Envfile from .env files and code" },
            new("checklist fixed label up wraps to action", "checklist", "<up><enter>") { Choices = Three, InitialNames = ["a", "b"], SubmitLabel = "Recreate Envfile", Message = "Recreate Envfile from .env files and code" },
            new("checklist space on action", "checklist", "<down><down><down><space><enter>") { Choices = Three, InitialNames = ["a"], SubmitLabel = "Go" },
            new("checklist a toggles", "checklist", "a<down><down><down><enter>") { Choices = Three, SubmitLabel = "Go" },
            new("checklist ctrl-c", "checklist", "<ctrl-c>") { Choices = Three, InitialNames = ["a"], SubmitLabel = "Go" },
            new("checklist escape", "checklist", "<esc>") { Choices = Three, SubmitLabel = "Go" },
            new("checklist no enter", "checklist", "<space>") { Choices = Three, SubmitLabel = "Go" },
            new("checklist digit", "checklist", "3<down><down><down><enter>") { Choices = Three, SubmitLabel = "Go" },
            new("checklist submit none selected", "checklist", "<down><down><down><enter>") { Choices = Three, SubmitLabel = "Go" },

            // password
            new("password type enter", "password", "secret<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password lf", "password", "secret<lf>") { Message = "KEY", Prefix = "◇", Separator = "=" },
            new("password empty enter", "password", "<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password backspace", "password", "abc<bs><bs>d<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password backspace at start", "password", "<bs>a<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password ctrl-c", "password", "ab<ctrl-c>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password escape", "password", "ab<esc>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password no enter", "password", "abc") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password empty input", "password", string.Empty) { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password unicode", "password", "héllo ⛨ wörld<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password cursor moves", "password", "abc<left><left>X<right>Y<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password home end", "password", "abc<home>X<end>Y<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password delete", "password", "abc<left><del><enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password ctrl-w ctrl-k ctrl-v", "password", "ab cd<ctrl-w><ctrl-v><ctrl-v>X<ctrl-a><ctrl-k><enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password ctrl-a ctrl-e ctrl-b ctrl-f", "password", "abc<ctrl-a>X<ctrl-e>Y<ctrl-b>Z<ctrl-f><enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password ctrl-x toggles cursor", "password", "abc<ctrl-x>X<ctrl-x>Y<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password tab shift-tab", "password", "abc<tab><s-tab>d<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password up down alert", "password", "a<up><down>b<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password alt key", "password", "a<alt-b>b<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password ctrl-d at end", "password", "abc<ctrl-d><enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password ctrl-g resets", "password", "abc<ctrl-g>d<enter>") { Message = "KEY", Prefix = "◈", Separator = "=" },
            new("password bitwarden prompt", "password", "pw<enter>") { Message = "Bitwarden master password", Prefix = "◇", Separator = "=" },

            // input
            new("input type enter", "input", "hello world<enter>") { Message = "Send feedback" },
            new("input empty enter", "input", "<enter>") { Message = "Send feedback" },
            new("input backspace and cursor", "input", "helo<left><left>l<right><right><bs>o<enter>") { Message = "Send feedback" },
            new("input ctrl-c", "input", "he<ctrl-c>") { Message = "Send feedback" },
            new("input no enter", "input", "he") { Message = "Send feedback" },
            new("input unicode", "input", "héllo ⛨<enter>") { Message = "Send feedback" },
            new("input home end", "input", "bc<home>a<end>d<enter>") { Message = "Send feedback" },
            new("input cut and paste", "input", "ab cd<ctrl-w><ctrl-v><ctrl-v><enter>") { Message = "Send feedback" },
            new("input tab alerts", "input", "a<tab>b<enter>") { Message = "Send feedback" },

            // confirm
            new("confirm enter default no", "confirm", "<enter>") { Message = "Proceed" },
            new("confirm enter default yes", "confirm", "<enter>") { Message = "Proceed", InitialBool = true },
            new("confirm y", "confirm", "y") { Message = "Proceed" },
            new("confirm n", "confirm", "n") { Message = "Proceed", InitialBool = true },
            new("confirm x alerts", "confirm", "x<enter>") { Message = "Proceed" },
            new("confirm ctrl-c", "confirm", "<ctrl-c>") { Message = "Proceed" }
        };

        return list.Select(scenario => new object[] { scenario });
    }

    private static JsonObject ToSpec(Scenario scenario)
    {
        var spec = new JsonObject
        {
            ["kind"] = scenario.Kind,
            ["message"] = scenario.Message,
            ["choices"] = new JsonArray([.. scenario.Choices.Select(choice => (JsonNode)new JsonObject
            {
                ["name"] = choice.Name, ["value"] = choice.Value, ["disabled"] = choice.Disabled
            })])
        };
        void Set(string name, JsonNode? value)
        {
            if (value is not null)
            {
                spec[name] = value;
            }
        }

        Set("prefix", scenario.Prefix);
        Set("separator", scenario.Separator);
        Set("initial", scenario.Initial);
        Set("backValue", scenario.BackValue);
        Set("submitLabel", scenario.SubmitLabel);
        Set("labelMode", scenario.LabelMode);
        Set("columns", scenario.Columns);
        Set("rows", scenario.Rows);
        spec["configured"] = scenario.Configured;
        spec["currentFilter"] = scenario.CurrentFilter;
        spec["currentIgnore"] = scenario.CurrentIgnore;
        spec["initialBool"] = scenario.InitialBool;
        if (scenario.InitialNames is { } names)
        {
            spec["initialNames"] = new JsonArray([.. names.Select(name => (JsonNode)name)]);
        }

        return spec;
    }

    private static JsonObject RunPort(Scenario scenario)
    {
        var output = new FakeOutput(scenario.Columns, scenario.Rows);
        Prompts.PromptOutput = output;
        Prompts.KeySource = new FakeKeys(Keys(scenario.Keys));
        Prompts.InputEnded = () => throw new EndedException();
        var choices = scenario.Choices.Select(choice => new Prompts.Choice(choice.Name, choice.Value, choice.Disabled)).ToArray();
        var result = new JsonObject();
        try
        {
            switch (scenario.Kind)
            {
                case "select" or "custody":
                    string? selected = Prompts.Select(new Prompts.SelectRequest(scenario.Message, choices)
                    {
                        Navigation = scenario.Kind == "custody",
                        BackValue = scenario.BackValue,
                        Initial = scenario.Initial,
                        Prefix = scenario.Prefix
                    });
                    result["result"] = selected;
                    break;
                case "multiselect" or "checklist":
                    Func<IReadOnlyList<string>, string>? function = scenario.LabelMode == "protect" ? ProtectLabel(scenario) : null;
                    IReadOnlyList<string>? values = Prompts.Multiselect(new Prompts.MultiselectRequest(scenario.Message, choices)
                    {
                        Initial = scenario.InitialNames ?? [],
                        SubmitLabel = scenario.Kind == "checklist" && function is null ? scenario.SubmitLabel : null,
                        SubmitLabelFunction = function
                    });
                    result["result"] = new JsonArray([.. values!.Select(value => (JsonNode)value)]);
                    break;
                case "password":
                    result["result"] = Prompts.Password(scenario.Message, scenario.Prefix!, scenario.Separator!);
                    break;
                case "input":
                    result["result"] = Prompts.InputText(scenario.Message);
                    break;
                case "confirm":
                    result["result"] = Prompts.Confirm(scenario.Message, scenario.InitialBool);
                    break;
            }
        }
        catch (PromptCancelledException)
        {
            result["error"] = "PROMPT_CANCELLED";
        }
        catch (EndedException)
        {
            result["ended"] = true;
        }

        var ordered = new JsonObject { ["out"] = output.Text.ToString() };
        foreach ((string key, JsonNode? value) in result.ToArray())
        {
            result.Remove(key);
            ordered[key] = value;
        }

        return ordered;
    }

    private static Func<IReadOnlyList<string>, string> ProtectLabel(Scenario scenario)
    {
        return selected =>
        {
            bool Has(string key) => selected.Contains(key);
            if (scenario.Configured && Has("filter") == scenario.CurrentFilter && Has("ignore") == scenario.CurrentIgnore)
            {
                return "Done";
            }

            if (selected.Count == 0)
            {
                return "Remove protections";
            }

            return scenario.Configured ? "Apply changes" : "Install protections";
        };
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task PortedPromptsWriteTheSameBytesAsEnquirer(Scenario scenario)
    {
        if (!fixture.IsAvailable)
        {
            return;
        }

        string specFile = Path.Combine(Path.GetTempPath(), $"enquirer-spec-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(specFile, ToSpec(scenario).ToJsonString());
        string oracle = Path.Combine(AppContext.BaseDirectory, "oracle", "enquirer.cjs");
        try
        {
            CliResult node = await fixture.RunNodeFileWithInputAsync(oracle, Keys(scenario.Keys), specFile);
            node.ExitCode.ShouldBe(0, node.StandardError);

            JsonObject expected = JsonNode.Parse(node.StandardOutput.Trim().Split('\n')[^1])!.AsObject();
            JsonObject actual;
            lock (typeof(Prompts))
            {
                try
                {
                    actual = RunPort(scenario);
                }
                finally
                {
                    Prompts.ResetForTests();
                }
            }

            string want = Show(expected["out"]!.GetValue<string>());
            string got = Show(actual["out"]!.GetValue<string>());
            int at = 0;
            while (at < want.Length && at < got.Length && want[at] == got[at])
            {
                at++;
            }

            if (want != got)
            {
                string Around(string text) => text[Math.Max(0, at - 80)..Math.Min(text.Length, at + 160)];
                throw new Xunit.Sdk.XunitException($"{scenario.Name}: first difference at {at} of {want.Length}/{got.Length}{Environment.NewLine}node: {Around(want)}{Environment.NewLine}port: {Around(got)}");
            }

            JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(expected), scenario.Name);
        }
        finally
        {
            File.Delete(specFile);
        }
    }

    /// <summary>Escape characters made visible, for readable failures.</summary>
    private static string Show(string text)
    {
        return text.Replace("\u001b", "ESC").Replace("\u0007", "BEL").Replace("\r", "\\r");
    }
}
