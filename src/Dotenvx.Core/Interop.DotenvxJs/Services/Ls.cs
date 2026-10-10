using Dotenvx.Interop.DotenvxJs.Helpers;
using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/resolvers/ls.js (moved there from src/lib/services/ls.js by this pin)</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Ls(string directory = "./", string[]? envFile = null, string[]? excludeEnvFile = null)
{
    private static readonly Func<string, string[]?, string[]?, Ls> DefaultFactory =
        (directory, envFile, excludeEnvFile) => new Ls(directory, envFile, excludeEnvFile);

    // path.resolve: '' is the working directory, and a blank name (" ") is just another, usually missing, directory
    private readonly string _cwd = directory.Length == 0
        ? NodePath.Resolve(".")
        : string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(NodePath.Resolve("."), directory)
            : NodePath.Resolve(directory);
    private readonly string[] _ignore = ["node_modules/**", "**/node_modules/**", ".git/**", "**/.git/**"];

    // Matches ls.js's DEFAULT_EXCLUDED_DIRECTORY_EXTENSIONS - the Fdir crawler never descends into
    // node_modules/.git or a directory whose extension matches one of these (macOS bundle formats),
    // so files inside are never even candidates. This port originally only filtered .env* files
    // AFTER a full unrestricted tree walk via the glob-based _ignore list above, which happened to
    // still exclude node_modules/.git (both are covered by those globs too) but had NO equivalent
    // for the bundle extensions at all - a .env* file inside e.g. a `.photoslibrary` directory would
    // be listed here but never seen by Node. Fixed by pruning at the directory-walk level directly,
    // matching LsAction.cs's own already-correct CountCrawledDirectories exclusion set.
    private static readonly HashSet<string> ExcludedDirectoryExtensions =
        [".app", ".key", ".numbers", ".pages", ".photoslibrary"];

    protected Ls() : this("./")
    {
        // For unit tests
    }

    public static Func<string, string[]?, string[]?, Ls> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual string[] Run()
    {
        return FilePaths();
    }

    private string[] FilePaths()
    {
        string[] excluded = Exclude();
        // dot: true matches ls.js's own real `match(patterns, { dot: true })` options - see
        // IndexJs.Picomatch's doc comment for why the default changed to dot:false everywhere else.
        Func<string, bool> exclude = Picomatch(excluded, dot: true);
        Func<string, bool> include = Picomatch(Patterns(), excluded, dot: true);

        // Enumerate every file once (pruning excluded directories during the walk, matching
        // upstream's Fdir crawler) and let the "**/"-prefixed picomatch globs do the actual
        // filtering, rather than passing each user pattern to Directory.GetFiles as its own OS
        // search pattern - "**/" isn't valid .NET search-pattern syntax, only valid picomatch/glob
        // syntax.
        return EnumerateFiles(_cwd)
            .Select(file => Path.GetRelativePath(_cwd, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !exclude(path) && include(path))
            .Distinct()
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Fdir skips directories it cannot read
    private static readonly EnumerationOptions Inaccessible = new() { IgnoreInaccessible = true, AttributesToSkip = 0 }; // AttributesToSkip 0: the default skips Hidden, which on Unix is every dot file

    public static bool IsLink(string directory)
    {
        return new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
    {
        // Fdir crawls a missing directory (or a file given as the directory) as empty: `ls nonexistent` reports 0 files
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (string file in Directory.GetFiles(directory, "*", Inaccessible))
        {
            yield return file;
        }

        foreach (string subdirectory in Directory.GetDirectories(directory, "*", Inaccessible))
        {
            // Fdir does not follow symbolic links: a link to a directory is reported like a file and never entered
            if (IsLink(subdirectory))
            {
                yield return subdirectory;

                continue;
            }

            string name = Path.GetFileName(subdirectory);
            if (name is "node_modules" or ".git" ||
                ExcludedDirectoryExtensions.Contains(Path.GetExtension(subdirectory).ToLowerInvariant()))
            {
                continue;
            }

            foreach (string file in EnumerateFiles(subdirectory))
            {
                yield return file;
            }
        }
    }

    private string[] Patterns()
    {
        string[] value = envFile is { Length: > 0 } ? envFile : [".env*"];
        return value.Select(part => $"**/{part}").ToArray();
    }

    private string[] ExcludePatterns()
    {
        return (excludeEnvFile ?? []).Select(part => $"**/{part}").ToArray();
    }

    private string[] Exclude()
    {
        string[] excludePatterns = ExcludePatterns();
        return excludePatterns.Length > 0
            ? _ignore.Concat(excludePatterns).ToArray()
            : _ignore;
    }
}