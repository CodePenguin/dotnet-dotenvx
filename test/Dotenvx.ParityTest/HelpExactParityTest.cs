using System.Text.Json;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - the exact-bytes `--help` check that replaced the structural-only comparison in
/// <see cref="HelpTextParityTest" />. `src/Dotenvx/Help/CommanderHelp.cs` lays out rows dumped from the real commander tree
/// (`node scripts/gen-help-spec.js`) with commander's own algorithm, so the whole help text, including the "Better
/// Security" / "Examples" trailers, must equal what Node prints for every command path and for `help &lt;command&gt;`.
/// The command list is read from the generated spec; Node's live output is the oracle, so a stale spec fails here.
/// </remarks>
public class HelpExactParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static List<string[]> CommandPaths()
    {
        string directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "src", "Dotenvx", "Help", "help-spec.json")))
        {
            directory = Path.GetDirectoryName(directory) ?? throw new FileNotFoundException("help-spec.json");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "src", "Dotenvx", "Help", "help-spec.json")));
        var paths = new List<string[]>();

        void Walk(JsonElement command, string[] path)
        {
            paths.Add(path);
            foreach (JsonElement child in command.GetProperty("children").EnumerateArray())
            {
                Walk(child, [.. path, child.GetProperty("name").GetString()!]);
            }
        }

        Walk(document.RootElement, []);

        return paths;
    }

    /// <remarks>The checked-in spec must be what `node scripts/gen-help-spec.js` produces from the pinned upstream today.</remarks>
    [Fact]
    public async Task HelpSpecIsUpToDate()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "scripts", "gen-help-spec.js")))
        {
            directory = Path.GetDirectoryName(directory) ?? throw new FileNotFoundException("scripts/gen-help-spec.js");
        }

        string specPath = Path.Combine(directory, "src", "Dotenvx", "Help", "help-spec.json");
        string before = File.ReadAllText(specPath);
        CliResult result = await Fixture.RunNodeScriptAsync(
            $"require({System.Text.Json.JsonSerializer.Serialize(Path.Combine(directory, "scripts", "gen-help-spec.js"))})");
        string after = File.ReadAllText(specPath);
        File.WriteAllText(specPath, before);

        result.ExitCode.ShouldBe(0, result.StandardError);
        after.ShouldBe(before, "help-spec.json is stale: run node scripts/gen-help-spec.js and commit it");
    }

    [Fact]
    public async Task HelpOfEveryCommandIsByteIdentical()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        List<string> differences = [];
        foreach (string[] path in CommandPaths())
        {
            string[] args = [.. path, "--help"];
            (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, args);
            Compare(string.Join(' ', args), node, dotnet, differences);
        }

        differences.ShouldBeEmpty();
    }

    [Fact]
    public async Task HelpCommandIsByteIdentical()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        List<string> differences = [];
        foreach (string[] path in CommandPaths().Where(path => path.Length == 1).Append([]))
        {
            string[] args = ["help", .. path];
            (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, args);
            Compare(string.Join(' ', args), node, dotnet, differences);
        }

        differences.ShouldBeEmpty();
    }

    /// <remarks>An unknown command is dynamic upstream (`rotate` was removed from upstream): commander still answers -h/--help with the root help.</remarks>
    [Theory]
    [InlineData("rotate", "--help")]
    [InlineData("foo", "-h")]
    [InlineData("foo", "bar", "--help")]
    [InlineData("rotate")]
    public async Task UnknownCommandIsHandledLikeNode(params string[] args)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        List<string> differences = [];
        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, args);
        Compare(string.Join(' ', args), node, dotnet, differences);

        differences.ShouldBeEmpty();
    }

    private static void Compare(string label, CliResult node, CliResult dotnet, List<string> differences)
    {
        if (node.ExitCode != dotnet.ExitCode)
        {
            differences.Add($"[{label}] exit code: node {node.ExitCode}, dotnet {dotnet.ExitCode}");
        }

        if (node.StandardOutput != dotnet.StandardOutput)
        {
            differences.Add($"[{label}] stdout differs:\n--- node\n{node.StandardOutput}\n--- dotnet\n{dotnet.StandardOutput}");
        }
    }
}
