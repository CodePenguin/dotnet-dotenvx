using Shouldly;

using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Core.Test.Interop.PicomatchJs;

/// <remarks>Reference source: dotenvx/tests/lib/picomatch.test.js</remarks>
public class IndexJsTest
{
    [Fact]
    public void Picomatch_NullGlobsAndEmptyIgnore_ReturnsTrueForAll()
    {
        Func<string, bool> matcher = Picomatch(null, []);

        matcher("any/path").ShouldBeTrue();
        matcher("").ShouldBeTrue();
    }

    [Fact]
    public void Picomatch_SingleAsterisk_MatchesAnyFile()
    {
        Func<string, bool> matcher = Picomatch(["*"]);

        matcher("file.txt").ShouldBeTrue();
        matcher("file.jpg").ShouldBeTrue();
        matcher("dir/file.txt").ShouldBeFalse(); // * does not match dir/
    }

    [Fact]
    public void Picomatch_QuestionMark_MatchesSingleCharacter()
    {
        Func<string, bool> matcher = Picomatch(["file.?xt"]);

        matcher("file.txt").ShouldBeTrue();
        matcher("file.ext").ShouldBeTrue();
        matcher("file.xt").ShouldBeFalse();
        matcher("file.txtt").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_DoubleAsterisk_MatchesAnyDepth()
    {
        Func<string, bool> matcher = Picomatch(["**/temp"]);

        matcher("temp").ShouldBeTrue();
        matcher("dir/temp").ShouldBeTrue();
        matcher("dir/sub/temp").ShouldBeTrue();
        matcher("temp/file").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_CharacterClass_MatchesSpecifiedCharacters()
    {
        Func<string, bool> matcher = Picomatch(["file.[tj]xt"]);

        matcher("file.txt").ShouldBeTrue();
        matcher("file.jxt").ShouldBeTrue();
        matcher("file.ext").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_SingleAsterisk_WithIgnore_ExcludesMatchingPatterns()
    {
        Func<string, bool> matcher = Picomatch(["*"], ["*.log"]);

        matcher("file.txt").ShouldBeTrue();
        matcher("file.log").ShouldBeFalse();
        matcher("dir/file.txt").ShouldBeFalse(); // * does not match dir/
    }

    [Fact]
    public void Picomatch_MultipleIncludes_MatchesAny()
    {
        Func<string, bool> matcher = Picomatch(["*.txt", "*.jpg"]);

        matcher("file.txt").ShouldBeTrue();
        matcher("file.jpg").ShouldBeTrue();
        matcher("file.png").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_MultipleIgnores_ExcludesAnyMatching()
    {
        Func<string, bool> matcher = Picomatch(["*"], ["*.log", "*.tmp"]);

        matcher("file.txt").ShouldBeTrue();
        matcher("file.log").ShouldBeFalse();
        matcher("file.tmp").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_DirectoryPatterns_MatchesDirectories()
    {
        Func<string, bool> matcher = Picomatch(["dir/"]);

        matcher("dir/").ShouldBeTrue();
        matcher("dir").ShouldBeFalse();
        matcher("dir/file.txt").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_ComplexPattern_MatchesCorrectly()
    {
        Func<string, bool> matcher = Picomatch(["src/**/*.js"]);

        matcher("src/app.js").ShouldBeTrue();
        matcher("src/lib/app.js").ShouldBeTrue();
        matcher("src/lib/utils/app.js").ShouldBeTrue();
        matcher("src/app.ts").ShouldBeFalse();
        matcher("lib/app.js").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_EmptyGlobsAndIgnore_ReturnsFalse()
    {
        Func<string, bool> matcher = Picomatch([], []);

        matcher("any/path").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_NullIgnore_TreatsAsEmpty()
    {
        // ReSharper disable once RedundantArgumentDefaultValue
        Func<string, bool> matcher = Picomatch(["*"], null);

        matcher("file.txt").ShouldBeTrue();
    }

    /// <remarks>
    /// Not from a JS test - found 2026-10-02 via a static audit cross-checking this reimplementation
    /// against the real, live-callable `@dotenvx/primitives` `match`/`scan` exports it stands in for.
    /// `dot` defaults to false (confirmed live via `scan(src, { ik: ['*'] })` on a file with a
    /// `.SECRET` key: Node excludes it) - a bare wildcard never matches a leading dot unless the
    /// caller opts in, matching every key-filtering call site's real upstream behavior
    /// (encrypt/decrypt/rotate/get's -k/--include-key/--exclude-key).
    /// </remarks>
    [Fact]
    public void Picomatch_DefaultDot_WildcardDoesNotMatchLeadingDot()
    {
        Func<string, bool> matcher = Picomatch(["*"]);

        matcher(".SECRET").ShouldBeFalse();
        matcher("FOO").ShouldBeTrue();
    }

    [Fact]
    public void Picomatch_DotTrue_WildcardMatchesLeadingDot()
    {
        Func<string, bool> matcher = Picomatch(["*"], dot: true);

        matcher(".SECRET").ShouldBeTrue();
        matcher("FOO").ShouldBeTrue();
    }

    [Fact]
    public void Picomatch_DefaultDot_LiteralLeadingDotInPatternStillMatches()
    {
        Func<string, bool> matcher = Picomatch([".*"]);

        matcher(".SECRET").ShouldBeTrue();
        matcher(".ABC").ShouldBeTrue();
        matcher("FOO").ShouldBeFalse();
    }

    [Fact]
    public void Picomatch_DefaultDot_AppliesToIgnoreListToo()
    {
        Func<string, bool> matcher = Picomatch(["*"], ["*"], dot: true);

        // dot:true applies to both the include and exclude lists identically, so a wildcard
        // exclude DOES catch a dot-prefixed name here.
        matcher(".SECRET").ShouldBeFalse();

        Func<string, bool> defaultDotMatcher = Picomatch(["FOO", ".SECRET"], ["*"]);

        // With the real default (dot:false), a wildcard exclude does NOT catch a dot-prefixed
        // name, so it stays included if the include list separately names it literally.
        defaultDotMatcher(".SECRET").ShouldBeTrue();
        defaultDotMatcher("FOO").ShouldBeFalse();
    }
}
