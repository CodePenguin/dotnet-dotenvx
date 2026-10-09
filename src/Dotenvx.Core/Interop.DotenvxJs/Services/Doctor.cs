using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <summary>One possible dotenv loader call found by <see cref="Doctor" />.</summary>
public record DoctorFinding(string Lang, string Filepath, int Line, string Code, string Msg);

/// <remarks>
/// Reference source: dotenvx/src/lib/services/doctor.js. The crawl mirrors `new Fdir().withRelativePaths().exclude(...)`:
/// the exclude callback receives the ABSOLUTE directory path with a trailing separator (so on Windows the `**/x/**`
/// globs see backslashes and match nothing, exactly as the Node CLI behaves there), and the returned filepaths are
/// relative with the platform separator, sorted with JS's default UTF-16 code unit order.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Doctor
{
    private sealed record Pattern(string Lang, Regex File, Regex Loader, string Msg);

    private static readonly Pattern[] Patterns =
    [
        new("Python", new Regex(@"\.py$"), new Regex(@"load_dotenv\s*\([^)]*\)"), "found python-dotenv load call"),
        new("Python", new Regex(@"\.py$"), new Regex(@"dotenv_values\s*\([^)]*\)"), "found python-dotenv values call"),

        new("Node", new Regex(@"\.(js|cjs|mjs|ts)$"), new Regex(@"require\(['""]dotenv['""]\)\.config\s*\([^)]*\)"),
            "found dotenv config require call"),
        new("Node", new Regex(@"\.(js|cjs|mjs|ts)$"), new Regex(@"import\s+['""]dotenv/config['""]"),
            "found dotenv/config import"),
        new("Node", new Regex(@"\.(js|cjs|mjs|ts)$"), new Regex(@"dotenv\.config\s*\([^)]*\)"),
            "found dotenv config call"),

        new("Ruby", new Regex(@"\.rb$"), new Regex(@"Dotenv\.(load|overload)\s*\([^)]*\)"),
            "found ruby dotenv load call"),
        new("Go", new Regex(@"\.go$"), new Regex(@"godotenv\.(Load|Overload)\s*\([^)]*\)"), "found godotenv load call"),
        new("PHP", new Regex(@"\.php$"), new Regex(@"Dotenv\\Dotenv::create\w*\s*\([^)]*\)"),
            "found vlucas/phpdotenv loader"),

        new("Rust", new Regex(@"\.rs$"), new Regex(@"dotenvy?::dotenv\s*\([^)]*\)"), "found Rust dotenv load call"),
        new("Java", new Regex(@"\.java$"), new Regex(@"Dotenv\.(load|configure)\s*\([^)]*\)"),
            "found java-dotenv load call"),
        new("Kotlin", new Regex(@"\.kt$"), new Regex(@"Dotenv\.(load|configure)\s*\([^)]*\)"),
            "found java-dotenv load call"),
        new(".NET", new Regex(@"\.(cs|fs|vb)$"), new Regex(@"(?:DotNetEnv\.)?Env\.Load\s*\([^)]*\)"),
            "found DotNetEnv load call")
    ];

    private static readonly Func<string, Doctor> DefaultFactory = directory => new Doctor(directory);

    private readonly string _cwd;
    private readonly Func<string, bool> _exclude;

    public Doctor(string directory = "./")
    {
        _cwd = NodePath.Resolve(directory);
        _exclude = Match([
            "**/node_modules/**",
            "**/.git/**",
            "**/vendor/**",
            "**/dist/**",
            "**/build/**",
            "**/coverage/**",
            "**/.next/**"
        ]);
    }

    protected Doctor() : this("./")
    {
        // For unit tests
    }

    public static Func<string, Doctor> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual DoctorFinding[] Run()
    {
        var findings = new List<DoctorFinding>();

        foreach (string filepath in Filepaths())
        {
            findings.AddRange(ScanFile(filepath));
        }

        return [.. findings];
    }

    internal string[] Filepaths()
    {
        var filepaths = new List<string>();
        Crawl(_cwd, "", filepaths);

        return [.. filepaths.Where(filepath => PatternsFor(filepath).Length > 0).Order(StringComparer.Ordinal)];
    }

    private void Crawl(string directory, string relativeDirectory, List<string> filepaths)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(directory))
        {
            filepaths.Add(relativeDirectory + Path.GetFileName(file));
        }

        foreach (string subdirectory in Directory.GetDirectories(directory))
        {
            string name = Path.GetFileName(subdirectory);
            string absolute = subdirectory + Path.DirectorySeparatorChar;
            if (Ignored(absolute))
            {
                continue;
            }

            Crawl(subdirectory, relativeDirectory + name + Path.DirectorySeparatorChar, filepaths);
        }
    }

    internal DoctorFinding[] ScanFile(string relativeFilepath)
    {
        string src = Fsx.ReadFileX(Path.Combine(_cwd, relativeFilepath));
        string[] lines = Regex.Split(src, "\r?\n");
        var findings = new List<DoctorFinding>();

        for (int index = 0; index < lines.Length; index++)
        {
            foreach (Pattern pattern in PatternsFor(relativeFilepath))
            {
                System.Text.RegularExpressions.Match match = pattern.Loader.Match(lines[index]);
                if (match.Success)
                {
                    findings.Add(new DoctorFinding(pattern.Lang, relativeFilepath, index + 1, match.Value.Trim(),
                        pattern.Msg));
                }
            }
        }

        return [.. findings];
    }

    private static Pattern[] PatternsFor(string filepath)
    {
        return [.. Patterns.Where(pattern => pattern.File.IsMatch(filepath))];
    }

    private bool Ignored(string filepath)
    {
        return _exclude(filepath);
    }
}
