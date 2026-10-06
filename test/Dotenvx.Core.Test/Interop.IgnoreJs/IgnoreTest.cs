using Dotenvx.Interop.IgnoreJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.IgnoreJs;

/// <remarks>
/// No upstream `ignore.test.js` exists at this pin (confirmed via find - a stale reference, same
/// pattern as the 2026-09-24 stale-doc-comment sweep documented elsewhere in docs/porting-traps.md). `ignore` is
/// a real, live-callable `@dotenvx/tooling` export (`node -e "require('@dotenvx/tooling').ignore()..."`),
/// used by `prebuild.js`/`precommit.js` to check whether a file is docker/gitignored - this port's
/// `Ignore`/`IgnoreRule` is a from-scratch reimplementation verified against that live oracle, not a
/// port of a readable upstream test file.
/// </remarks>
public class IgnoreTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        // Cleanup temp files
        foreach (string file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Add_SinglePattern_ShouldIgnoreMatchingPath()
    {
        Ignore ig = new();
        ig.Add(".DS_Store");

        ig.Ignores(".DS_Store").ShouldBeTrue();
        ig.Ignores("README.md").ShouldBeFalse();
    }

    [Fact]
    public void Add_WildcardPattern_ShouldIgnoreMatchingFiles()
    {
        Ignore ig = new();
        ig.Add("*.log");

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("error.log").ShouldBeTrue();
        ig.Ignores("test.txt").ShouldBeFalse();
    }

    /// <remarks>
    /// Rewritten 2026-10-02 - the old assertion (`ig.Ignores("node_modules").ShouldBeTrue()`) locked
    /// in a bug: confirmed live via `require('@dotenvx/tooling').ignore().add('node_modules/')` that
    /// a directory-only (trailing-slash) pattern does NOT match the bare name itself, only paths
    /// actually nested inside it - see `IgnoreRule.ConvertToRegex`'s own doc comment.
    /// </remarks>
    [Fact]
    public void Add_DirectoryPattern_ShouldIgnoreDirectory()
    {
        Ignore ig = new();
        ig.Add("node_modules/");

        ig.Ignores("node_modules/").ShouldBeTrue();
        ig.Ignores("node_modules").ShouldBeFalse();
        ig.Ignores("node_modules/package.json").ShouldBeTrue();
        ig.Ignores("src/").ShouldBeFalse();
    }

    [Fact]
    public void Add_NegationPattern_ShouldNotIgnore()
    {
        Ignore ig = new();
        ig.Add("*.log");
        ig.Add("!important.log");

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("important.log").ShouldBeFalse();
    }

    [Fact]
    public void Add_MultipleLines_ShouldProcessAll()
    {
        Ignore ig = new();
        const string patterns =
            """
            *.log
            *.tmp
            node_modules/
            .env
            """;
        ig.Add(patterns);

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("temp.tmp").ShouldBeTrue();
        ig.Ignores("node_modules/").ShouldBeTrue();
        ig.Ignores(".env").ShouldBeTrue();
        ig.Ignores("README.md").ShouldBeFalse();
    }

    [Fact]
    public void Add_Comments_ShouldIgnoreComments()
    {
        Ignore ig = new();
        const string patterns =
            """
            # This is a comment
            *.log
            # Another comment
            *.tmp
            """;
        ig.Add(patterns);

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("temp.tmp").ShouldBeTrue();
        ig.Ignores("# This is a comment").ShouldBeFalse();
    }

    [Fact]
    public void Add_EmptyLines_ShouldIgnoreEmptyLines()
    {
        Ignore ig = new();
        const string patterns =
            """

            *.log

            *.tmp

            """;
        ig.Add(patterns);

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("temp.tmp").ShouldBeTrue();
    }

    [Fact]
    public void Add_DoubleAsterisk_ShouldMatchAnyDirectory()
    {
        Ignore ig = new();
        ig.Add("**/temp");

        ig.Ignores("temp").ShouldBeTrue();
        ig.Ignores("src/temp").ShouldBeTrue();
        ig.Ignores("src/lib/temp").ShouldBeTrue();
    }

    [Fact]
    public void Add_TrailingDoubleAsterisk_ShouldMatchAnythingAfter()
    {
        Ignore ig = new();
        ig.Add("logs/**");

        ig.Ignores("logs/test.log").ShouldBeTrue();
        ig.Ignores("logs/2024/jan/test.log").ShouldBeTrue();
        ig.Ignores("other/test.log").ShouldBeFalse();
    }

    [Fact]
    public void Add_RootedPattern_ShouldOnlyMatchAtRoot()
    {
        Ignore ig = new();
        ig.Add("/.env");

        ig.Ignores(".env").ShouldBeTrue();
        ig.Ignores("src/.env").ShouldBeFalse();
    }

    [Fact]
    public void Add_PatternWithSlash_ShouldMatchPath()
    {
        Ignore ig = new();
        ig.Add("src/*.js");

        ig.Ignores("src/test.js").ShouldBeTrue();
        ig.Ignores("lib/test.js").ShouldBeFalse();
        ig.Ignores("test.js").ShouldBeFalse();
    }

    [Fact]
    public void Add_QuestionMark_ShouldMatchSingleCharacter()
    {
        Ignore ig = new();
        ig.Add("?.log");

        ig.Ignores("a.log").ShouldBeTrue();
        ig.Ignores("1.log").ShouldBeTrue();
        ig.Ignores("ab.log").ShouldBeFalse();
    }

    [Fact]
    public void Add_Array_ShouldProcessAllPatterns()
    {
        Ignore ig = new();
        string[] patterns = ["*.log", "*.tmp", ".env"];
        ig.Add(patterns);

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("temp.tmp").ShouldBeTrue();
        ig.Ignores(".env").ShouldBeTrue();
        ig.Ignores("README.md").ShouldBeFalse();
    }

    [Fact]
    public void Add_Chainable_ShouldReturnSelf()
    {
        Ignore ig = new();
        Ignore result = ig.Add("*.log").Add("*.tmp");

        result.ShouldBe(ig);
        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("temp.tmp").ShouldBeTrue();
    }

    [Fact]
    public void Ignores_NullOrEmpty_ShouldReturnFalse()
    {
        Ignore ig = new();
        ig.Add("*.log");

        ig.Ignores("").ShouldBeFalse();
        ig.Ignores("   ").ShouldBeFalse();
    }

    [Fact]
    public void Ignores_WindowsPath_ShouldNormalizeAndMatch()
    {
        Ignore ig = new();
        ig.Add("src/*.js");

        ig.Ignores(@"src\test.js").ShouldBeTrue();
        ig.Ignores("src/test.js").ShouldBeTrue();
    }

    [Fact]
    public void Filter_ListOfPaths_ShouldReturnNotIgnored()
    {
        Ignore ig = new();
        ig.Add("*.log");
        ig.Add(".env");
        string[] paths = ["README.md", "test.log", ".env", "package.json"];
        List<string> filtered = ig.Filter(paths).ToList();

        filtered.Count.ShouldBe(2);
        filtered.ShouldContain("README.md");
        filtered.ShouldContain("package.json");
        filtered.ShouldNotContain("test.log");
        filtered.ShouldNotContain(".env");
    }

    [Fact]
    public void FromFile_ExistingFile_ShouldLoadPatterns()
    {
        string tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);
        File.WriteAllText(tempFile, "*.log\n.env\nnode_modules/");
        Ignore ig = Ignore.FromFile(tempFile);

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores(".env").ShouldBeTrue();
        ig.Ignores("node_modules/").ShouldBeTrue();
        ig.Ignores("README.md").ShouldBeFalse();
    }

    [Fact]
    public void FromFile_NonExistentFile_ShouldReturnEmptyIgnore()
    {
        Ignore ig = Ignore.FromFile("nonexistent.txt");

        ig.Ignores("anything.txt").ShouldBeFalse();
    }

    [Fact]
    public void Add_ComplexGitignoreExample_ShouldWorkCorrectly()
    {
        Ignore ig = new();
        const string gitignore =
            """
            # Logs
            logs
            *.log
            npm-debug.log*

            # Dependencies
            node_modules/
            jspm_packages/

            # Optional npm cache directory
            .npm

            # dotenv environment variables file
            .env
            .env.test
            !.env.example

            # Build output
            dist/
            build/
            *.min.js
            """;
        ig.Add(gitignore);

        ig.Ignores("logs").ShouldBeTrue();
        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("npm-debug.log").ShouldBeTrue();
        ig.Ignores("node_modules/").ShouldBeTrue();
        ig.Ignores("node_modules/package/index.js").ShouldBeTrue();
        ig.Ignores(".npm").ShouldBeTrue();
        ig.Ignores(".env").ShouldBeTrue();
        ig.Ignores(".env.test").ShouldBeTrue();
        ig.Ignores(".env.example").ShouldBeFalse(); // Negated
        ig.Ignores("dist/").ShouldBeTrue();
        ig.Ignores("build/").ShouldBeTrue();
        ig.Ignores("app.min.js").ShouldBeTrue();
        ig.Ignores("README.md").ShouldBeFalse();
    }

    [Fact]
    public void Ignores_DotEnvKeys_RealWorldScenario()
    {
        Ignore ig = new();
        const string gitignore =
            """
            # Environment files
            .env
            .env.local
            .env.keys

            # Logs
            *.log
            """;
        ig.Add(gitignore);

        ig.Ignores(".env.keys").ShouldBeTrue();
        ig.Ignores(".env").ShouldBeTrue();
        ig.Ignores(".env.local").ShouldBeTrue();
        ig.Ignores(".env.example").ShouldBeFalse();
    }

    [Fact]
    public void Ignores_BasenameMatching_ShouldMatchInAnyDirectory()
    {
        Ignore ig = new();
        ig.Add("test.txt"); // No slash, so matches anywhere

        ig.Ignores("test.txt").ShouldBeTrue();
        ig.Ignores("src/test.txt").ShouldBeTrue();
        ig.Ignores("src/lib/test.txt").ShouldBeTrue();
    }

    [Fact]
    public void Ignores_LastMatchingRuleWins()
    {
        Ignore ig = new();
        ig.Add("*.log"); // Ignore all .log files
        ig.Add("!debug.log"); // Don't ignore debug.log
        ig.Add("debug.log"); // But ignore it again

        ig.Ignores("test.log").ShouldBeTrue();
        ig.Ignores("debug.log").ShouldBeTrue(); // Last rule wins
    }

    [Fact]
    public void Add_WhitespaceOnly_ShouldBeIgnored()
    {
        Ignore ig = new();
        ig.Add("   \n   \t   ");

        ig.Ignores("anything.txt").ShouldBeFalse();
    }

    /// <remarks>
    /// Not from a JS test - found 2026-10-02 via a static audit cross-checking this reimplementation
    /// against the real `@dotenvx/tooling` `ignore` export. A bare (no `/`) non-anchored pattern must
    /// match as a whole path segment at any depth, including nested content under it - confirmed live
    /// that the old regex (`(?:^|\/){pattern}`, no trailing boundary) failed `node_modules/foo`.
    /// </remarks>
    [Fact]
    public void Ignores_BasenamePattern_MatchesNestedContentUnderIt()
    {
        Ignore ig = new();
        ig.Add("node_modules");

        ig.Ignores("node_modules").ShouldBeTrue();
        ig.Ignores("node_modules/foo").ShouldBeTrue();
        ig.Ignores("a/node_modules/b").ShouldBeTrue();
        ig.Ignores("anode_modules").ShouldBeFalse();
    }

    /// <remarks>
    /// Not from a JS test - found 2026-10-02, same audit as above. An anchored pattern (containing
    /// `/` anywhere except as a single trailing character) must ALSO match everything nested under
    /// it, not just the exact path - confirmed live that the old regex had no such suffix, so
    /// `/build` failed to match `build/file`.
    /// </remarks>
    [Fact]
    public void Ignores_AnchoredPattern_MatchesNestedContentUnderIt()
    {
        Ignore ig = new();
        ig.Add("/build");

        ig.Ignores("build").ShouldBeTrue();
        ig.Ignores("build/file").ShouldBeTrue();
        ig.Ignores("sub/build").ShouldBeFalse();
        ig.Ignores("buildx").ShouldBeFalse();
    }

    /// <remarks>
    /// Not from a JS test - found 2026-10-02, same audit as above. `dir/**` requires a literal,
    /// mandatory slash before the wildcard - confirmed live that the old regex's optional `(?:/.*)?`
    /// wrongly matched the bare `dir` itself.
    /// </remarks>
    [Fact]
    public void Ignores_TrailingDoubleAsterisk_RequiresLiteralSlashNotBareDirectory()
    {
        Ignore ig = new();
        ig.Add("foo/**");

        ig.Ignores("foo").ShouldBeFalse();
        ig.Ignores("foo/a").ShouldBeTrue();
        ig.Ignores("sub/foo/a").ShouldBeFalse();
    }

    [Fact]
    public void Ignores_NestedDirectories_ShouldWorkCorrectly()
    {
        Ignore ig = new();
        ig.Add("**/node_modules");

        ig.Ignores("node_modules").ShouldBeTrue();
        ig.Ignores("project/node_modules").ShouldBeTrue();
        ig.Ignores("project/subproject/node_modules").ShouldBeTrue();
        ig.Ignores("src/node_modules_backup").ShouldBeFalse();
    }
}