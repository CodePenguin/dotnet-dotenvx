using System.CommandLine;
using System.CommandLine.Parsing;

namespace Dotenvx.Helpers;

/// <remarks>
/// commander drops EXTRA positional arguments silently but always rejects an unknown OPTION
/// (`error: unknown option '--x'`, exit 1). System.CommandLine's `TreatUnmatchedTokensAsErrors = false` gives the
/// first behavior but, because it binds a dash-prefixed unknown token to the next open positional slot, also
/// swallows the second (trap #38: `get --made-up HELLO` became `[MISSING_KEY] missing key (--made-up)`).
/// <see cref="Apply" /> keeps the lenient mode and <see cref="FindUnknownOption" /> restores the rejection: any
/// dash-prefixed argument token before a `--` separator is an unknown option, exactly as commander treats it.
/// </remarks>
public static class UnknownOptionGuard
{
    public static void Apply(Command command)
    {
        command.TreatUnmatchedTokensAsErrors = false;

        foreach (Command child in command.Subcommands)
        {
            Apply(child);
        }
    }

    /// <summary>The first token commander would call an unknown option, or null.</summary>
    public static string? FindUnknownOption(ParseResult parseResult)
    {
        foreach (Token token in parseResult.Tokens)
        {
            if (token.Type == TokenType.DoubleDash)
            {
                return null;
            }

            if (token.Type == TokenType.Argument && token.Value.Length > 1 && token.Value[0] == '-')
            {
                return token.Value;
            }
        }

        return null;
    }
}
