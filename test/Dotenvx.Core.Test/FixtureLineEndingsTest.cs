using Shouldly;

namespace Dotenvx.Core.Test;

/// <remarks>
/// The upstream fixtures in the `dotenvx` submodule are LF, and the tests compare multi-line values with them. The repository's
/// `.gitattributes` cannot reach into the submodule, so a Windows clone with `core.autocrlf=true` checks them out as CRLF and a
/// whole class of tests then fails in confusing ways. This says so directly (CONTRIBUTING.md, "Line endings").
/// </remarks>
public class FixtureLineEndingsTest
{
    /// <summary>Fixtures that are not LF text upstream: a deliberate CRLF file and two files in other encodings.</summary>
    private static readonly string[] NotLf = [".env.crlf", ".env.latin1", ".env.utf16le"];

    [Fact]
    public void SubmoduleFixturesAreCheckedOutWithLf()
    {
        string tests = Path.Combine(FindRepoRoot(), "dotenvx", "tests");

        string[] crlf = [.. Directory.EnumerateFiles(tests, ".env*", SearchOption.AllDirectories)
            .Where(file => !NotLf.Contains(Path.GetFileName(file)))
            .Where(file => File.ReadAllText(file).Contains('\r'))
            .Select(file => Path.GetRelativePath(tests, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)];

        crlf.ShouldBeEmpty("the dotenvx submodule was checked out with CRLF line endings: run `git -C dotenvx config core.autocrlf false`, " +
                           "then `git -C dotenvx rm --cached -r . -q && git -C dotenvx reset --hard` (see CONTRIBUTING.md, \"Line endings\")");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Dotenvx.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root (no Dotenvx.sln above the test binaries)");
    }
}
