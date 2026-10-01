using System.Text.RegularExpressions;

namespace Dotenvx.ParityTest;

/// <summary>An "Options:" (or "Arguments:") section entry: every alias token plus its description.</summary>
public sealed record HelpEntry(string[] Aliases, string Description)
{
    public bool HasAlias(string alias)
    {
        return Aliases.Contains(alias);
    }
}

/// <remarks>
/// Parses the "Options:" section of a `--help` output into structured entries, tolerating the very
/// different rendering commander.js (Node) and System.CommandLine (.NET) each use - different
/// headers, different column widths, different line-wrapping - so <see cref="HelpTextParityTest"/>
/// can compare the actual REGISTERED FLAGS/DESCRIPTIONS rather than raw text, which would never
/// byte-match between the two frameworks even when a command is perfectly ported.
/// </remarks>
public static class HelpTextParser
{
    private static readonly Regex EntryStartRegex = new(@"^  (?<rest>-\S.*)$");
    private static readonly Regex AliasDescSplitRegex = new(@"^(?<aliases>\S.*?)(?:  +(?<desc>\S.*))?$");

    /// <summary>
    /// Extracts every option entry under the first "Options:" header in <paramref name="helpText"/>.
    /// A new entry starts at a line with exactly two leading spaces followed by a `-`; any other
    /// non-blank line is treated as a wrapped continuation of the previous entry's description.
    /// </summary>
    public static List<HelpEntry> ParseOptions(string helpText)
    {
        string[] lines = helpText.Replace("\r\n", "\n").Split('\n');
        List<HelpEntry> entries = [];

        int start = Array.FindIndex(lines, l => l.Trim() == "Options:");
        if (start < 0)
        {
            return entries;
        }

        string? currentAliasPart = null;
        List<string> currentDescParts = [];

        void Flush()
        {
            if (currentAliasPart == null)
            {
                return;
            }

            string[] aliases = currentAliasPart
                .Split(',')
                .Select(a => a.Trim())
                .Select(StripArgumentPlaceholder)
                .Where(a => a.Length > 0)
                .ToArray();

            if (aliases.Length > 0)
            {
                entries.Add(new HelpEntry(aliases, string.Join(' ', currentDescParts).Trim()));
            }

            currentAliasPart = null;
            currentDescParts = [];
        }

        for (int i = start + 1; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();

            if (line.Trim().Length == 0)
            {
                break; // blank line ends the Options section
            }

            Match entryMatch = EntryStartRegex.Match(line);
            if (entryMatch.Success)
            {
                Flush();
                Match splitMatch = AliasDescSplitRegex.Match(entryMatch.Groups["rest"].Value);
                currentAliasPart = splitMatch.Groups["aliases"].Value;
                if (splitMatch.Groups["desc"].Success)
                {
                    currentDescParts.Add(splitMatch.Groups["desc"].Value.Trim());
                }
            }
            else if (currentAliasPart != null)
            {
                currentDescParts.Add(line.Trim());
            }
        }

        Flush();
        return entries;
    }

    /// <summary>Strips a trailing `&lt;...&gt;` or `[...]` argument placeholder from an alias token.</summary>
    private static string StripArgumentPlaceholder(string alias)
    {
        int bracket = alias.IndexOfAny(['<', '[']);
        return bracket < 0 ? alias : alias[..bracket].TrimEnd();
    }
}
