using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Help;

using Shouldly;

namespace dotnet_dotenvx.Test;

/// <remarks>
/// Not from a JS test - the command tree of this port must equal upstream's (help-spec.json is dumped from the real
/// commander tree by scripts/gen-help-spec.js): same commands (hidden ones included, aliases count as names), same
/// options per command, same arguments. Catches leftovers such as the removed `rotate` and missing flags without
/// relying on any scenario list.
/// </remarks>
public class CommandTreeParityTest : EnvironmentTestBase
{
    // System.CommandLine structural additions: -? on --help, and the recursive global options that commander only
    // shows at the root
    private static readonly HashSet<string> DotnetOnly =
        ["-?", "-V", "--version", "-l", "--log-level", "-q", "--quiet", "-v", "--verbose", "-d", "--debug", "-h", "--help"];

    private static readonly HashSet<string> RootOnlyUpstream =
        ["-V", "--version", "-l", "--log-level", "-q", "--quiet", "-v", "--verbose", "-d", "--debug", "-h", "--help"];

    private DotenvxRootCommand BuildRoot()
    {
        var root = new DotenvxRootCommand(
            new ArmorCommand(MockLogger),
            new CurlAction(MockLogger),
            new DecryptAction(MockLogger),
            new DelAction(MockLogger),
            new DoctorAction(MockLogger),
            new EncryptAction(MockLogger),
            new ExtCommand(
                new GenexampleAction(MockLogger), new GitignoreAction(MockLogger), new LsAction(MockLogger),
                new PrebuildAction(MockLogger), new PrecommitAction(MockLogger), new ScanAction(MockLogger),
                MockLogger),
            new GenexampleAction(MockLogger),
            new GetAction(MockLogger),
            new GitignoreAction(MockLogger),
            new global::Dotenvx.Actions.HelpAction(),
            new KeyPairAction(MockLogger),
            new LockCommand(MockLogger),
            new LoginAction(MockLogger),
            new LogoutAction(MockLogger),
            new LsAction(MockLogger),
            new NativeCommand(MockLogger),
            new PrebuildAction(MockLogger),
            new PrecommitAction(MockLogger),
            new RunAction(MockLogger),
            new SetAction(MockLogger),
            new UpdateAction(),
            new ValidateAction(MockLogger));
        return root;
    }

    private static HashSet<string> Names(Command command)
    {
        return [command.Name, .. command.Aliases];
    }

    private static HashSet<string> Flags(Option option)
    {
        return [option.Name, .. option.Aliases];
    }

    private static IEnumerable<string> TermFlags(string term)
    {
        return term.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Where(part => part.StartsWith('-'));
    }

    [Fact]
    public void TheCommandTreeMatchesUpstream()
    {
        var differences = new List<string>();
        Compare(BuildRoot(), CommanderHelp.RootSpec, "dotenvx", differences);

        differences.ShouldBeEmpty();
    }

    private static void Compare(Command ours, HelpSpec spec, string path, List<string> differences)
    {
        // (upstream lists `help [command]` as a command argument we model as an optional argument)
        // upstream's dec/enc are separate commands; here they are aliases of decrypt/encrypt
        var ourNames = ours.Subcommands.SelectMany(c => Names(c)).Where(n => n != "help" || path == "dotenvx").ToHashSet();
        var upstreamNames = spec.Children.Select(c => c.Name).ToHashSet();
        foreach (string name in upstreamNames.Except(ourNames))
        {
            differences.Add($"{path}: upstream command '{name}' is missing");
        }

        foreach (string name in ourNames.Except(upstreamNames))
        {
            differences.Add($"{path}: extra command '{name}' (not upstream)");
        }

        var ourFlags = ours.Options.SelectMany(o => Flags(o)).Where(f => !DotnetOnly.Contains(f) || (path == "dotenvx" && RootOnlyUpstream.Contains(f)))
            .ToHashSet();
        var upstreamFlags = spec.Options.SelectMany(o => TermFlags(o.Term)).ToHashSet();
        if (path != "dotenvx")
        {
            upstreamFlags.ExceptWith(RootOnlyUpstream);
        }

        foreach (string flag in upstreamFlags.Except(ourFlags).Except(["--pp"]))
        {
            differences.Add($"{path}: upstream option '{flag}' is missing");
        }

        foreach (string flag in ourFlags.Except(upstreamFlags))
        {
            differences.Add($"{path}: extra option '{flag}' (not upstream)");
        }

        int ourArguments = ours.Arguments.Count(a => !a.Hidden);
        if (ourArguments != spec.Arguments.Count && path is not ("dotenvx" or "dotenvx help"))
        {
            differences.Add($"{path}: {ourArguments} arguments, upstream {spec.Arguments.Count}");
        }

        foreach (HelpSpec child in spec.Children)
        {
            Command? match = ours.Subcommands.FirstOrDefault(c => Names(c).Contains(child.Name));
            if (match is not null)
            {
                Compare(match, child, $"{path} {child.Name}", differences);
            }
        }
    }
}
