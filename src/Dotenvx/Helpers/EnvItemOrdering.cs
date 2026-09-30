using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Helpers;

/// <remarks>
/// `dotenvx.js` pushes every `-e`/`-f` occurrence into ONE shared array, in the exact order they
/// appear on argv (both flags' commander callbacks push into the same array) - this matters
/// because each item is later injected into a single shared `processEnv` dict in sequence: with
/// `--overload`, a LATER item wins for a duplicate key; without it, the FIRST item wins. This
/// port's `run`/`validate` actions used to build their explicit env-item list by reading each
/// option's own accumulated array independently and concatenating (every `-e` before every `-f`,
/// regardless of actual command-line order) - confirmed live this can flip an outcome: `.env` has
/// `KEY=` (empty), `.env.example` requires `KEY`; `dotenvx validate -f .env -e "KEY=x" --overload`
/// passes on real Node (the `-e` comes after `-f`, so its value wins) but failed on this port
/// (which always processed `-e` before `-f`, so `.env`'s empty value won instead).
///
/// Walks `ParseResult.Tokens` (the raw, pre-binding token stream, still in argv order) instead of
/// each option's already-grouped value array, stopping entirely at a `--` separator so a trailing
/// command's own arguments (e.g. `run`'s child command, which may itself contain literal `-e`/`-f`-
/// looking text) are never misattributed to these options.
/// </remarks>
internal static class EnvItemOrdering
{
    public static List<EnvItem> BuildExplicitEnvItemsInOrder(
        ParseResult parseResult, Option<string[]?> envOption, Option<string[]?> envFileOption)
    {
        HashSet<string> envNames = [envOption.Name, .. envOption.Aliases];
        HashSet<string> envFileNames = [envFileOption.Name, .. envFileOption.Aliases];

        List<EnvItem> items = [];
        EnvType? currentType = null;

        foreach (Token token in parseResult.Tokens)
        {
            if (token.Type == TokenType.DoubleDash)
            {
                break;
            }

            if (token.Type == TokenType.Option)
            {
                if (envNames.Contains(token.Value))
                {
                    currentType = EnvType.Env;
                }
                else if (envFileNames.Contains(token.Value))
                {
                    currentType = EnvType.EnvFile;
                }
                else
                {
                    currentType = null;
                }

                continue;
            }

            if (currentType == EnvType.Env)
            {
                items.Add(new EnvItem { Type = EnvType.Env, Value = token.Value });
            }
            else if (currentType == EnvType.EnvFile)
            {
                items.AddRange(SplitCommaSeparatedEnvFiles([token.Value])
                    .Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
            }
        }

        return items;
    }
}
