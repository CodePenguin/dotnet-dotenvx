using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Structural `--help` parity check, added 2026-10-01 in place of continuing ad hoc manual/agent
/// sweeps for this bug class. A raw byte-diff of `--help` text is not viable - System.CommandLine
/// and commander.js render help completely differently (different headers, column widths, line
/// wrapping, and .NET shows every recursive global option on every subcommand while Node only shows
/// them at the root) - so this parses each side's "Options:" section into structured
/// (aliases, description) entries via <see cref="HelpTextParser"/> and compares those instead.
///
/// This test was written by first manually surveying every shared command's real `--help` output
/// on both sides, which immediately found two real bugs before the automated test even existed:
/// `validate`'s `-e`/`-f` options were missing their `(default: [])` suffix (a copy/paste omission
/// relative to `run`/`get`'s identical options), and `get` was missing the `--pp` alias for
/// `--pretty-print` entirely (`get --pp` fell through to the `[KEY]` positional and failed with
/// MISSING_KEY) - the same gap already fixed for `keypair` but never applied to `get`, confirmed via
/// `dotenvx.js` registering `--pp` as a literal separate option in exactly two places. Both fixed
/// alongside this test landing.
///
/// Known, deliberate exclusions (not bugs - see CLAUDE.md for the full rationale on each):
/// - `DotnetOnlyAliases`: System.CommandLine structural artifacts (`-?` auto-added to `--help`;
///   every recursive global option `-l`/`-q`/`-v`/`-d`/`-V` rendered per-subcommand, where Node only
///   shows them at the root) plus `--file`, a deliberately forward-ported alias for `-f`/
///   `--env-file` ahead of this project's v2.24.1 pin (see the ParityTest traps list's very first
///   entry).
/// </remarks>
public class HelpTextParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    // `--help` normally prints commander's layout from help-spec.json (see HelpExactParityTest); this test checks the
    // real System.CommandLine option definitions, so it asks the .NET CLI for its own native help text
    private static readonly Dictionary<string, string> NativeHelp = new() { ["DOTENVX_NATIVE_HELP"] = "1" };

    private static readonly HashSet<string> DotnetOnlyAliases =
    [
        "-?", "-V", "--version", "-l", "--log-level", "-q", "--quiet", "-v", "--verbose", "-d", "--debug", "--file"
    ];

    // "--pp" (a third alias on get/keypair's --pretty-print option, confirmed functionally working
    // live) is excluded here, not added to DotnetOnlyAliases, because it's a NODE option this port
    // genuinely implements - System.CommandLine's default HelpBuilder only ever renders the first
    // two aliases of an Option in the compact "Options:" listing (confirmed via a scratch probe
    // against the real installed package: a 3-alias Option always shows just 2), so it can never be
    // parsed out of .NET's --help text at all, with no public API to change that rendering (same
    // class of HelpBuilder limitation as CLAUDE.md's trap #17). A real, permanent byte-level
    // divergence, but a cosmetic one - the flag works identically on both sides.
    private static readonly HashSet<string> KnownUnrenderedDotnetAliases = ["--pp"];

    [Theory]
    [InlineData("run")]
    [InlineData("get")]
    [InlineData("set")]
    [InlineData("del")]
    [InlineData("encrypt")]
    [InlineData("decrypt")]
    [InlineData("keypair")]
    [InlineData("ls")]
    [InlineData("validate")]
    [InlineData("gitignore")]
    [InlineData("genexample")]
    [InlineData("precommit")]
    [InlineData("prebuild")]
    public async Task CommandOptionsMatchNode(string command)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, NativeHelp, command, "--help");

        AssertOptionsMatch(node.StandardOutput, dotnet.StandardOutput);
    }

    /// <remarks>`scan` is `ext`-only - no top-level `dotenvx scan` exists upstream at this pin.</remarks>
    [Fact]
    public async Task ScanOptionsMatchNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, NativeHelp, "ext", "scan", "--help");

        AssertOptionsMatch(node.StandardOutput, dotnet.StandardOutput);
    }

    private static void AssertOptionsMatch(string nodeHelp, string dotnetHelp)
    {
        List<HelpEntry> nodeEntries = HelpTextParser.ParseOptions(nodeHelp);
        List<HelpEntry> dotnetEntries = HelpTextParser.ParseOptions(dotnetHelp);

        nodeEntries.ShouldNotBeEmpty("expected at least one option in Node's --help output");
        dotnetEntries.ShouldNotBeEmpty("expected at least one option in .NET's --help output");

        List<string> errors = [];
        HashSet<HelpEntry> matchedDotnetEntries = [];

        foreach (HelpEntry nodeEntry in nodeEntries)
        {
            if (nodeEntry.Aliases.All(a => KnownUnrenderedDotnetAliases.Contains(a)))
            {
                continue;
            }

            HelpEntry? match = dotnetEntries.FirstOrDefault(d => d.Aliases.Any(nodeEntry.HasAlias));
            if (match == null)
            {
                errors.Add($"Node has '{string.Join(", ", nodeEntry.Aliases)}' with no matching .NET option");
                continue;
            }

            matchedDotnetEntries.Add(match);

            string[] unexpectedAliases = match.Aliases
                .Except(nodeEntry.Aliases)
                .Except(DotnetOnlyAliases)
                .ToArray();
            if (unexpectedAliases.Length > 0)
            {
                errors.Add($"'{string.Join(", ", nodeEntry.Aliases)}': .NET has unexpected extra " +
                           $"alias(es) [{string.Join(", ", unexpectedAliases)}]");
            }

            string normalizedNodeDescription = Normalize(nodeEntry.Description);
            string normalizedDotnetDescription = Normalize(match.Description);
            if (normalizedNodeDescription != normalizedDotnetDescription)
            {
                errors.Add($"'{string.Join(", ", nodeEntry.Aliases)}' description differs:" +
                           $"\n  Node: {normalizedNodeDescription}\n  .NET: {normalizedDotnetDescription}");
            }
        }

        foreach (HelpEntry dotnetEntry in dotnetEntries)
        {
            if (matchedDotnetEntries.Contains(dotnetEntry) || dotnetEntry.Aliases.Any(DotnetOnlyAliases.Contains))
            {
                continue;
            }

            errors.Add($".NET has '{string.Join(", ", dotnetEntry.Aliases)}' with no matching Node option");
        }

        errors.ShouldBeEmpty(string.Join('\n', errors));
    }

    private static string Normalize(string text)
    {
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <remarks>
    /// Not from a JS test - found porting "default help lists direct utility commands ...": three commands (`set`, `encrypt`,
    /// `decrypt`) had made-up descriptions. Every command's one-line description must match Node's.
    /// </remarks>
    [Theory]
    [InlineData("run")]
    [InlineData("get")]
    [InlineData("set")]
    [InlineData("del")]
    [InlineData("encrypt")]
    [InlineData("decrypt")]
    [InlineData("keypair")]
    [InlineData("ls")]
    [InlineData("validate")]
    [InlineData("gitignore")]
    [InlineData("genexample")]
    [InlineData("precommit")]
    [InlineData("prebuild")]
    public async Task CommandDescriptionMatchesNode(string command)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, NativeHelp, command, "--help");

        string nodeDescription = FirstLineAfter(node.StandardOutput, line => line.StartsWith("Usage:"));
        string dotnetDescription = FirstLineAfter(dotnet.StandardOutput, line => line.Trim() == "Description:");

        dotnetDescription.ShouldBe(nodeDescription);
    }

    private static string FirstLineAfter(string help, Func<string, bool> header)
    {
        string[] lines = help.Replace("\r\n", "\n").Split('\n');
        int start = Array.FindIndex(lines, l => header(l));
        return lines.Skip(start + 1).Select(l => l.Trim()).First(l => l.Length > 0);
    }
}
