using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/ls.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Ls(string directory = "./", string[]? envFile = null, string[]? excludeEnvFile = null)
{
    private static readonly Func<string, string[]?, string[]?, Ls> DefaultFactory =
        (directory, envFile, excludeEnvFile) => new Ls(directory, envFile, excludeEnvFile);

    private readonly string _cwd = Path.GetFullPath(directory);
    private readonly string[] _ignore = ["node_modules/**", ".git/**"];

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
        List<string> files = [];
        string[] excluded = Exclude();
        Func<string, bool> exclude = Picomatch(excluded);
        Func<string, bool> include = Picomatch(Patterns(), excluded);

        foreach (string pattern in Patterns())
        {
            string[] found = Directory.GetFiles(_cwd, pattern, SearchOption.AllDirectories);
            files.AddRange(found
                .Select(file => Path.GetRelativePath(_cwd, file))
                .Where(path => !exclude(path) && include(path))
                .OrderBy(path => path));
        }

        return files.ToArray();
    }

    private string[] Patterns()
    {
        return envFile?.Select(part => part).ToArray() ?? [];
    }

    private string[] ExcludePatterns()
    {
        return excludeEnvFile?.Select(part => part).ToArray() ?? [];
    }

    private string[] Exclude()
    {
        string[] excludePatterns = ExcludePatterns();
        return excludePatterns.Length > 0
            ? _ignore.Concat(excludePatterns).ToArray()
            : _ignore;
    }
}