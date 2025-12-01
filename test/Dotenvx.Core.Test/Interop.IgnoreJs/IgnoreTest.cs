using Dotenvx.Interop.IgnoreJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.IgnoreJs;

/// <remarks>Reference source: dotenvx/tests/lib/ignore.test.js</remarks>
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

    [Fact]
    public void Add_DirectoryPattern_ShouldIgnoreDirectory()
    {
        Ignore ig = new();
        ig.Add("node_modules/");

        ig.Ignores("node_modules/").ShouldBeTrue();
        ig.Ignores("node_modules").ShouldBeTrue();
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