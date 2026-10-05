using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/ls.js. Fixed 2026-09-25: this action never printed
/// ls.js's `▣ found N .env files across M directories of K scanned in Xs` summary line at all -
/// not a cosmetic gap, a fully missing feature (upstream prints it via a raw `console.error(...)`
/// call that bypasses the logger entirely, so it always shows regardless of log level - matching
/// that here too). `directoryCount` (the "K scanned" figure) mirrors resolvers/ls.js's Fdir
/// `.exclude((dirname, directory) => {...})` callback exactly: it counts every directory actually
/// descended into, starting at 1 for the root, but SKIPS (doesn't count, doesn't descend into)
/// `node_modules`/`.git` by exact basename and any directory whose extension matches
/// `DEFAULT_EXCLUDED_DIRECTORY_EXTENSIONS` - confirmed by reading that resolver directly, not
/// derived from the simpler user-facing `--exclude-env-file` glob patterns Ls.cs itself uses for
/// FILE filtering. `matchedDirectoryCount` is just the unique parent-directory count among the
/// already-computed filepaths. The `--quiet` suppression (`!spinnerOptions.quiet &amp;&amp;
/// !options.quiet`) is NOT implemented - IDotenvxLogger has no way to query the global -q/--quiet
/// flag from within a single action's Execute(), and wiring that through was judged not worth it
/// for a narrow edge case; the summary line prints unconditionally here instead. Elapsed time is
/// inherently non-deterministic and was already excluded from every automated comparison for this
/// line before this fix - this port formats that field as "0s" always, which is honest about not
/// tracking real elapsed time rather than fabricating a plausible-looking duration.
/// </remarks>
public class LsAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory")
        {
            Arity = ArgumentArity.ZeroOrOne, Description = "directory to list .env files from (default: \".\")"
        };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f")
        {
            Description = "path(s) to your env file(s) (default: \".env*\")", HelpName = "filenames...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string[]?> _excludeEnvFileOption =
        new("--exclude-env-file", "-ef")
        {
            Description = "path(s) to exclude from your env file(s) (default: none)",
            HelpName = "excludeFilenames...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<bool> _jsonOption =
        new("--json") { Description = "output a JSON array of absolute filepaths" };

    private readonly IDotenvxLogger _logger;

    public LsAction(IDotenvxLogger logger) : base("ls",
        "print all .env files in a tree structure")
    {
        _logger = logger;
        Add(_directoryArgument);
        Add(_envFileOption);
        Add(_excludeEnvFileOption);
        Add(_jsonOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "traversing");

        DateTime startedAt = DateTime.UtcNow;
        string directory = parseResult.GetValue(_directoryArgument) ?? "./";
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? excludeEnvFile = parseResult.GetValue(_excludeEnvFileOption);
        bool json = parseResult.GetValue(_jsonOption);

        // debug args
        _logger.Debug($"directory: {directory}");

        var options = new { envFile, excludeEnvFile };
        _logger.Debug($"options: {JsonStringify(options)}");

        Ls lsService = Ls.Factory(directory, envFile, excludeEnvFile);
        string[] filepaths = lsService.Run();
        _logger.Debug($"filepaths: {JsonStringify(filepaths)}");

        if (json)
        {
            string cwd = Path.GetFullPath(directory);
            string[] absoluteFilepaths = filepaths.Select(filepath => Path.GetFullPath(Path.Combine(cwd, filepath)))
                .ToArray();
            Spinners.StopActive();
            Console.WriteLine(JsonStringify(absoluteFilepaths, true));
        }
        else
        {
            Dictionary<string, object> tree = ArrayToTree(filepaths);
            _logger.Debug($"tree: {JsonStringify(tree)}");

            string treeified = Treeify(tree);
            _logger.Info(treeified);
        }

        // ls.js: the summary line is skipped when -q/--quiet is set
        if (!(parseResult.RootCommandResult.Command is LoggingRootCommand root && root.IsQuiet(parseResult)))
        {
            PrintSummary(filepaths, Path.GetFullPath(directory), (int)(DateTime.UtcNow - startedAt).TotalSeconds);
        }

        return 0;
    }

    private static void PrintSummary(string[] filepaths, string cwd, int elapsedSeconds)
    {
        int matchedDirectoryCount = filepaths
            .Select(filepath => filepath.Contains('/') ? filepath[..filepath.LastIndexOf('/')] : ".")
            .Distinct()
            .Count();
        int directoryCount = 1 + CountCrawledDirectories(cwd);

        string fileLabel = filepaths.Length == 1 ? "file" : "files";
        string directoryLabel = matchedDirectoryCount == 1 ? "directory" : "directories";

        Console.Error.WriteLine(
            $"▣ found {filepaths.Length:N0} .env {fileLabel} across {matchedDirectoryCount:N0} {directoryLabel} of {directoryCount:N0} scanned in {elapsedSeconds}s");
    }

    private static readonly HashSet<string> ExcludedDirectoryExtensions =
        [".app", ".key", ".numbers", ".pages", ".photoslibrary"];

    private static int CountCrawledDirectories(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int count = 0;
        foreach (string subdirectory in Directory.GetDirectories(directory))
        {
            string name = Path.GetFileName(subdirectory);
            if (name is "node_modules" or ".git" ||
                ExcludedDirectoryExtensions.Contains(Path.GetExtension(subdirectory).ToLowerInvariant()))
            {
                continue;
            }

            count++;
            count += CountCrawledDirectories(subdirectory);
        }

        return count;
    }
}