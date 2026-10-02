using System.CommandLine;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/ext/gitignore.js. Upstream has NO try/catch at all -
/// an fsx write failure propagates uncaught (confirmed by reading the file directly) - so this
/// action deliberately doesn't add one either, matching CLAUDE.md's already-documented lesson
/// about not inventing error-handling upstream doesn't have.
/// </remarks>
public class GitignoreAction : Command
{
    private readonly IDotenvxLogger _logger;

    private readonly Option<string[]?> _patternOption =
        new("--pattern")
        {
            Description = "pattern(s) to gitignore (default: [\".env*\"])", HelpName = "patterns...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    public GitignoreAction(IDotenvxLogger logger) : base("gitignore", "append to .gitignore")
    {
        _logger = logger;
        Add(_patternOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        // System.CommandLine's Option<string[]?> resolves to a non-null EMPTY array when the
        // flag is omitted entirely (confirmed via a scratch reflection probe, 2026-09-24 - never
        // null), so a `?? [...]` fallback never triggers here; must check Length explicitly.
        string[]? rawPatterns = parseResult.GetValue(_patternOption);
        string[] patterns = rawPatterns is { Length: > 0 } ? rawPatterns : [".env*"];

        _logger.Debug($"options: {JsonStringify(new { pattern = patterns })}");

        Gitignore gitignoreService = Gitignore.Factory(patterns);
        GitignoreGenericResult[] results = gitignoreService.Run();

        foreach (GitignoreGenericResult result in results)
        {
            _logger.Verbose(result.VerboseMessage);

            if (result.Skipped)
            {
                // File doesn't exist and wasn't touched (e.g. .npmignore/.vercelignore when
                // absent) - gitignore.js's Generic.run() logs nothing at all in this case.
            }
            else if (result.Changed)
            {
                _logger.Success($"▣ ignored {string.Join(',', result.Patterns)} ({result.Filename})");
            }
            else
            {
                _logger.Info($"○ no change ({result.Filename})");
            }
        }

        return 0;
    }
}
