using System.Text.RegularExpressions;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Guards the options Node declares or passes along but never reads (docs/parity-checklist.md, accepted difference 13): `encoding`,
/// `DOTENV_KEY`, `noSpinner`, `logName` and `logVersion`. They are left out of <see cref="ConfigOptions" /> on purpose. This scans the
/// pinned upstream source for them and expects exactly today's occurrences, so a version bump that starts reading one of them fails
/// here and says what to port. Reads local files only; no Node is needed.
/// </remarks>
public class UnreadOptionsSentinelTest
{
    /// <summary>The option, the pattern that finds a use of it, and where upstream mentions it today (file to number of matches of the word, so `noSpinner: options.noSpinner` counts twice).</summary>
    public static IEnumerable<object[]> Options()
    {
        // main.js only hands noSpinner to the envs resolver, which does not read it
        yield return ["noSpinner", @"\bnoSpinner\b", new Dictionary<string, int> { ["lib/main.js"] = 2 }];

        // shared/logger.js stores them on the logger and nothing reads them back
        yield return ["logName", @"\blogName\b", new Dictionary<string, int> { ["shared/logger.js"] = 4 }];
        yield return ["logVersion", @"\blogVersion\b", new Dictionary<string, int> { ["shared/logger.js"] = 4 }];

        // declared in main.d.ts only
        yield return ["DOTENV_KEY", @"\bDOTENV_KEY\b", new Dictionary<string, int>()];
        yield return ["encoding", @"\boptions\??\.encoding\b", new Dictionary<string, int>()];
    }

    /// <remarks>Refer to docs/parity-checklist.md accepted difference 13</remarks>
    [Theory]
    [MemberData(nameof(Options))]
    public void UpstreamStillDoesNotReadTheOption(string option, string pattern, Dictionary<string, int> expected)
    {
        string source = Path.Combine(FindRepoRoot(), "dotenvx", "src");
        var regex = new Regex(pattern);

        Dictionary<string, int> found = Directory.EnumerateFiles(source, "*.js", SearchOption.AllDirectories)
            .Select(file => (File: Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/'), Count: regex.Matches(System.IO.File.ReadAllText(file)).Count))
            .Where(entry => entry.Count > 0)
            .ToDictionary(entry => entry.File, entry => entry.Count);

        found.OrderBy(pair => pair.Key).ShouldBe(expected.OrderBy(pair => pair.Key),
            $"upstream now uses `{option}` differently: port it (see docs/parity-checklist.md, accepted difference 13) and update this test");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(Path.Combine(dir.FullName, "Dotenvx.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root (no Dotenvx.sln above the test binaries)");
    }
}
