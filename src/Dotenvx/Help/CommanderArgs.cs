using System;
using System.Collections.Generic;
using System.Linq;

namespace Dotenvx.Help;

/// <remarks>
/// Reference source: commander's Command.parseOptions (bundled in @dotenvx/tooling). Two shapes differ from
/// System.CommandLine and are normalized before parsing, using the option terms in help-spec.json:
/// 1. an option with a required value takes the NEXT argument as its value even when that starts with a dash
///    (`get -f --overload` sets the env file to "--overload"); System.CommandLine would read it as an option;
/// 2. `-f=.env` / `-f:x` keep the separator in the value (commander: "=.env"), where System.CommandLine strips it.
/// </remarks>
public static class CommanderArgs
{
    public static string[] Normalize(string[] args)
    {
        return Normalize(args, out _);
    }

    /// <param name="args">The command line.</param>
    /// <param name="missingTerm">
    /// The upstream term (`-f, --env-file &lt;path&gt;`) of an option whose value commander would find missing because the
    /// next argument belongs to a level above (`get -f -q`), else null.
    /// </param>
    public static string[] Normalize(string[] args, out string? missingTerm)
    {
        missingTerm = null;
        var result = new List<string>();
        var chain = new List<HelpSpec> { CommanderHelp.RootSpec };
        bool positionalSeen = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--")
            {
                result.AddRange(args[i..]);

                break;
            }

            if (arg.Length > 2 && arg[0] == '-' && char.IsAsciiLetter(arg[1]) && arg[2] is '=' or ':' &&
                TakesValue(chain, arg[..2]) is not null)
            {
                // commander: -f=.env is option -f with the value "=.env"
                result.Add(arg[..2]);
                result.Add(arg[2..]);

                continue;
            }

            if (arg.Length > 2 && arg[0] == '-' && arg[1] != '-' && IsRootBoolean(arg[..2]) && !IsKnown(chain[^1], arg))
            {
                // commander (root level): a known boolean flag at the start of a cluster is taken out and the rest is
                // parsed again (`-dash` is -d, then the unknown `-ash`)
                result.Add(arg[..2]);
                args = [.. args[..i], "-" + arg[2..], .. args[(i + 1)..]];
                i--;

                continue;
            }

            if (arg.Length > 1 && arg[0] == '-')
            {
                string? term = TakesValue(chain, arg);
                int level = LevelOf(chain, arg);

                // commander parses level by level: the options of the levels above (the root's -q, -l, ...) are taken out of
                // the arguments first, so they are never the value of a deeper option (`get -f -q`: "argument missing")
                bool valueIsAncestorOption = i + 1 < args.Length && Enumerable.Range(0, Math.Max(level, 0))
                    .Any(above => IsKnown(chain[above], args[i + 1]));
                if (term is not null && term.Contains('<') && valueIsAncestorOption)
                {
                    missingTerm ??= term;
                }

                if (term is not null && term.Contains('<') && i + 1 < args.Length && !valueIsAncestorOption)
                {
                    // a required value is taken whatever it looks like (and is never a command name)
                    result.Add(args[i + 1].StartsWith('-') ? $"{arg}={args[i + 1]}" : arg);
                    if (!args[i + 1].StartsWith('-'))
                    {
                        result.Add(args[i + 1]);
                    }

                    i++;

                    continue;
                }

                result.Add(arg);

                continue;
            }

            if (!positionalSeen)
            {
                positionalSeen = true;
                HelpSpec? child = chain[^1].Children.FirstOrDefault(c => c.Name == arg);
                if (child is not null)
                {
                    chain.Add(child);
                    positionalSeen = false;
                }
            }

            result.Add(arg);
        }

        return [.. result];
    }

    private static bool IsRootBoolean(string flag)
    {
        return CommanderHelp.RootSpec.Options.Any(row => Flags(row).Contains(flag) && !row.Term.Contains('<') && !row.Term.Contains('['));
    }

    private static string[] Flags(HelpRow row)
    {
        return row.Term.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Where(part => part.StartsWith('-')).ToArray();
    }

    /// <summary>The shallowest level (0 = root) whose options contain exactly <paramref name="flag" />, or -1.</summary>
    private static int LevelOf(List<HelpSpec> chain, string flag)
    {
        for (int level = 0; level < chain.Count; level++)
        {
            if (chain[level].Options.Any(row => Flags(row).Contains(flag)))
            {
                return level;
            }
        }

        return -1;
    }

    /// <summary>commander's _findOption plus the single-dash cluster rule (`-qd` starts with the known `-q`).</summary>
    private static bool IsKnown(HelpSpec level, string token)
    {
        if (!token.StartsWith('-') || token == "--")
        {
            return false;
        }

        if (level.Options.Any(row => Flags(row).Contains(token)))
        {
            return true;
        }

        return token.Length > 2 && token[1] != '-' && level.Options.Any(row => Flags(row).Contains(token[..2]));
    }

    /// <summary>The upstream option term when <paramref name="flag" /> is exactly one of its flags and it takes a value.</summary>
    private static string? TakesValue(List<HelpSpec> chain, string flag)
    {
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            foreach (HelpRow row in chain[i].Options)
            {
                string[] parts = row.Term.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Contains(flag) && (row.Term.Contains('<') || row.Term.Contains('[')))
                {
                    return row.Term;
                }
            }
        }

        return null;
    }
}
