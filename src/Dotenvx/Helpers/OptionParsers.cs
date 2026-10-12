using System.Collections.Generic;
using System.CommandLine.Parsing;

namespace Dotenvx.Helpers;

/// <remarks>
/// `Option&lt;string[]?&gt;.AllowMultipleArgumentsPerToken = true` (needed for genuinely variadic
/// upstream flags - see docs/porting-traps.md, trap for that fix) makes the option a greedy token sink: it
/// keeps consuming every FOLLOWING token as another array value until it hits one that matches an
/// ALREADY-REGISTERED sibling option - confirmed via a scratch probe against the real installed
/// package that it does NOT stop at a token that merely LOOKS like an option (starts with `-`) if
/// that flag isn't actually registered anywhere. `dotenvx validate --ignore VALIDATION_FAILED
/// --strict` (a real, live-reproduced case - `validate` has no `--strict` option at all) got
/// silently absorbed into `--ignore`'s array as a bogus extra "error code" instead of being
/// rejected, when Node's commander always rejects an unrecognized `--flag`-shaped token
/// regardless of what preceded it (`error: unknown option '--strict'`).
///
/// This `CustomParser` closes that gap: by the time it runs, `ArgumentResult.Tokens` already
/// reflects the framework's own token-grouping decision (confirmed a REGISTERED sibling option
/// is correctly excluded before this ever sees it), so any token that still starts with `-` here
/// must be either a value nobody in this CLI ever legitimately types starting with a dash, or a
/// genuine unrecognized/mistyped flag - both are safe to reject the same way `TreatUnmatchedTokensAsErrors`'s
/// own default "Unrecognized command or argument" message does elsewhere in this CLI, keeping the
/// wording consistent.
/// </remarks>
internal static class OptionParsers
{
    public static string[] ParseMultiValue(ArgumentResult result)
    {
        List<string> values = [];
        foreach (Token token in result.Tokens)
        {
            if (token.Value.StartsWith('-'))
            {
                result.AddError($"Unrecognized command or argument '{token.Value}'.");
                continue;
            }

            values.Add(token.Value);
        }

        return values.ToArray();
    }
}
