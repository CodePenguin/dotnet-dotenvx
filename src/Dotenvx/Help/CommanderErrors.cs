using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Dotenvx.Help;

/// <remarks>
/// Reference source: commander's Command.unknownOption / optionMissingArgument and its suggestSimilar helper (bundled in
/// @dotenvx/tooling). Flag lists come from help-spec.json so the messages (`error: option '-f, --env-file &lt;path&gt;'
/// argument missing`, `(Did you mean --env?)`) match Node's exactly.
/// </remarks>
public static class CommanderErrors
{
    /// <summary>The command names as typed (upstream's `dec`/`enc` are separate commands, here aliases), below the root.</summary>
    public static List<string> PathOf(ParseResult parseResult)
    {
        var path = new List<string>();
        for (CommandResult? current = parseResult.CommandResult; current is not null and not { Command: RootCommand };
             current = current.Parent as CommandResult)
        {
            path.Insert(0, current.IdentifierToken?.Value ?? current.Command.Name);
        }

        return path;
    }

    private static IEnumerable<HelpSpec> Chain(IReadOnlyList<string> path)
    {
        for (int length = path.Count; length >= 0; length--)
        {
            HelpSpec? spec = CommanderHelp.Find(path.Take(length).ToList());
            if (spec is not null)
            {
                yield return spec;
            }
        }
    }

    /// <summary>`error: option '&lt;flags&gt;' argument missing` for the first option that lacks its value, or null.</summary>
    public static string? MissingArgument(ParseResult parseResult)
    {
        foreach (ParseError error in parseResult.Errors)
        {
            if (error.SymbolResult is not OptionResult { Option: { } option } optionResult || option.Arity.MinimumNumberOfValues == 0)
            {
                continue;
            }

            // a required option that was never given is `required option ... not specified`, not a missing value
            if (optionResult.IdentifierToken is null)
            {
                continue;
            }

            if (FindRow(parseResult, option) is { } row)
            {
                return $"error: option '{row.Term}' argument missing";
            }
        }

        return null;
    }

    /// <summary>`error: required option '&lt;flags&gt;' not specified` for the first required option that was not given, or null.</summary>
    public static string? RequiredOption(ParseResult parseResult)
    {
        foreach (Option option in parseResult.CommandResult.Command.Options.Where(option => option.Required))
        {
            if (parseResult.GetResult(option) is not { IdentifierToken: not null } && FindRow(parseResult, option) is { } row)
            {
                return $"error: required option '{row.Term}' not specified";
            }
        }

        return null;
    }

    /// <summary>`error: too many arguments for 'check'. Expected 0 arguments but got 1.` for a command registered with allowExcessArguments(false), or null.</summary>
    public static string? ExcessArguments(ParseResult parseResult)
    {
        HelpSpec? spec = CommanderHelp.Find(PathOf(parseResult));
        if (spec is null || spec.AllowExcessArguments || spec.LastArgumentVariadic)
        {
            return null;
        }

        int received = parseResult.CommandResult.Children.OfType<ArgumentResult>().Sum(argument => argument.Tokens.Count) +
                       parseResult.UnmatchedTokens.Count;
        if (received <= spec.ArgumentCount)
        {
            return null;
        }

        string plural = spec.ArgumentCount == 1 ? "" : "s";

        return $"error: too many arguments for '{spec.Name}'. Expected {spec.ArgumentCount} argument{plural} but got {received}.";
    }

    /// <summary>The upstream (term, description) row of <paramref name="option" /> for the command that was invoked.</summary>
    public static HelpRow? FindRow(ParseResult parseResult, Option option)
    {
        var flags = new HashSet<string>([option.Name, .. option.Aliases]);
        foreach (HelpSpec spec in Chain(PathOf(parseResult)))
        {
            HelpRow? row = spec.Options.FirstOrDefault(o => TermFlags(o.Term).Any(flags.Contains));
            if (row is not null)
            {
                return row;
            }
        }

        return null;
    }

    private static IEnumerable<string> TermFlags(string term)
    {
        return term.Split([',', ' ', '|'], StringSplitOptions.RemoveEmptyEntries).Where(part => part.StartsWith('-'));
    }

    /// <summary>`error: unknown option '&lt;flag&gt;'` plus commander's "Did you mean" line for `--long` flags.</summary>
    public static string UnknownOption(string flag, IReadOnlyList<string> path)
    {
        string suggestion = "";
        if (flag.StartsWith("--"))
        {
            List<string> candidates = Chain(path)
                .SelectMany(spec => spec.Options.SelectMany(o => TermFlags(o.Term)).Where(f => f.StartsWith("--")))
                .ToList();
            suggestion = SuggestSimilar(flag, candidates);
        }

        return $"error: unknown option '{flag}'{suggestion}";
    }

    /// <summary>commander's suggestSimilar.</summary>
    public static string SuggestSimilar(string word, IEnumerable<string> candidates)
    {
        List<string> unique = candidates.Distinct().ToList();
        if (unique.Count == 0)
        {
            return "";
        }

        bool searchingOptions = word.StartsWith("--");
        if (searchingOptions)
        {
            word = word[2..];
            unique = unique.Select(c => c[2..]).ToList();
        }

        var similar = new List<string>();
        int bestDistance = 3;
        foreach (string candidate in unique)
        {
            if (candidate.Length <= 1)
            {
                continue;
            }

            int distance = EditDistance(word, candidate);
            int length = Math.Max(word.Length, candidate.Length);
            if ((double)(length - distance) / length > 0.4)
            {
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    similar = [candidate];
                }
                else if (distance == bestDistance)
                {
                    similar.Add(candidate);
                }
            }
        }

        similar.Sort((a, b) => string.Compare(a, b, StringComparison.InvariantCulture));
        if (searchingOptions)
        {
            similar = similar.Select(c => $"--{c}").ToList();
        }

        return similar.Count switch
        {
            > 1 => $"\n(Did you mean one of {string.Join(", ", similar)}?)",
            1 => $"\n(Did you mean {similar[0]}?)",
            _ => ""
        };
    }

    /// <summary>Damerau-Levenshtein distance as commander computes it (a length difference above 3 short-circuits).</summary>
    private static int EditDistance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 3)
        {
            return Math.Max(a.Length, b.Length);
        }

        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (int j = 1; j <= b.Length; j++)
        {
            for (int i = 1; i <= a.Length; i++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
