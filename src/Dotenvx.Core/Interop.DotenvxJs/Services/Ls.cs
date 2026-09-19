using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/resolvers/ls.js (moved there from src/lib/services/ls.js by this pin)</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Ls(string directory = "./", string[]? envFile = null, string[]? excludeEnvFile = null)
{
    private static readonly Func<string, string[]?, string[]?, Ls> DefaultFactory =
        (directory, envFile, excludeEnvFile) => new Ls(directory, envFile, excludeEnvFile);

    private readonly string _cwd = Path.GetFullPath(directory);
    private readonly string[] _ignore = ["node_modules/**", "**/node_modules/**", ".git/**", "**/.git/**"];

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
        Func<string, bool> exclude = Picomatch(excluded);
        Func<string, bool> include = Picomatch(Patterns(), excluded);

        // Enumerate every file once and let the "**/"-prefixed picomatch globs do the actual
        // filtering (matching upstream's Fdir-crawl-then-filter approach), rather than passing
        // each user pattern to Directory.GetFiles as its own OS search pattern - "**/" isn't
        // valid .NET search-pattern syntax, only valid picomatch/glob syntax.
        return Directory.GetFiles(_cwd, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(_cwd, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !exclude(path) && include(path))
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
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