using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js</remarks>
public class GitignoreTest : EnvironmentTestBase
{
    /// <remarks>Refer to "gitignore calls Git, Docker, Npm, and Vercel"</remarks>
    [Fact]
    public void TestRunCallsGitDockerNpmAndVercelInOrder()
    {
        Fsx.ExistsSync = _ => false;

        GitignoreGenericResult[] results = new Gitignore([".env*"]).Run();

        results.Length.ShouldBe(4);
        results[0].Filename.ShouldBe(".gitignore");
        results[1].Filename.ShouldBe(".dockerignore");
        results[2].Filename.ShouldBe(".npmignore");
        results[3].Filename.ShouldBe(".vercelignore");
    }

    /// <remarks>Refer to "Generic class - constructor initializes correctly"</remarks>
    [Fact]
    public void TestGenericConstructorDefaults()
    {
        GitignoreGeneric generic = new(".gitignore", null, true);

        generic.Filename.ShouldBe(".gitignore");
        generic.Patterns.ShouldBe([".env*"]);
        generic.TouchFile.ShouldBeTrue();
    }

    /// <remarks>Refer to "Generic class - append method"</remarks>
    [Fact]
    public void TestGenericAppend()
    {
        string? appendedPath = null;
        string? appendedData = null;
        Fsx.AppendFileSync = (path, data) =>
        {
            appendedPath = path;
            appendedData = data;
        };

        new GitignoreGeneric(".gitignore").Append("TEST");

        appendedPath.ShouldBe(".gitignore");
        appendedData.ShouldBe("\nTEST");
    }

    /// <remarks>Refer to "Generic class - run method - creates file if it does not exist and touchFile is true"</remarks>
    [Fact]
    public void TestRunCreatesFileWhenMissingAndTouchFileTrue()
    {
        Fsx.ExistsSync = _ => false;
        string? writtenPath = null;
        string? writtenContent = null;
        Fsx.WriteFileX = (path, content) =>
        {
            writtenPath = path;
            writtenContent = content;
        };
        Fsx.ReadFileX = (_, _) => "";
        Fsx.AppendFileSync = (_, _) => { };

        GitignoreGenericResult result = new GitignoreGeneric(".gitignore", [".env*"], true).Run("add to .gitignore");

        writtenPath.ShouldBe(".gitignore");
        writtenContent.ShouldBe("");
        result.Skipped.ShouldBeFalse();
        result.Changed.ShouldBeTrue();
    }

    /// <remarks>Refer to "Generic class - run method - does nothing if file does not exist and touchFile is false"</remarks>
    [Fact]
    public void TestRunSkipsWhenMissingAndTouchFileFalse()
    {
        Fsx.ExistsSync = _ => false;
        bool wroteFile = false;
        Fsx.WriteFileX = (_, _) => wroteFile = true;

        GitignoreGenericResult result = new GitignoreGeneric(".npmignore").Run("add to .npmignore (if existing)");

        wroteFile.ShouldBeFalse();
        result.Skipped.ShouldBeTrue();
        result.Changed.ShouldBeFalse();
    }

    /// <remarks>Refer to "Generic class - run method - appends patterns to existing file"</remarks>
    [Fact]
    public void TestRunAppendsPatternToExistingFileMissingIt()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "some content\n";
        string? appended = null;
        Fsx.AppendFileSync = (_, data) => appended = data;

        GitignoreGenericResult result = new GitignoreGeneric(".gitignore", [".env.keys"], false).Run("add to .gitignore");

        appended.ShouldBe("\n.env.keys");
        result.Changed.ShouldBeTrue();
        result.Skipped.ShouldBeFalse();
    }

    /// <remarks>
    /// Not from a direct JS test - the "no change" (pattern already present) case, verified live
    /// against the real Node CLI (running `dotenvx gitignore` twice in a row produces
    /// "○ no change (.gitignore)" the second time).
    /// </remarks>
    [Fact]
    public void TestRunReportsNoChangeWhenPatternAlreadyPresent()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "\n.env*";
        bool appended = false;
        Fsx.AppendFileSync = (_, _) => appended = true;

        GitignoreGenericResult result = new GitignoreGeneric(".gitignore", [".env*"], true).Run("add to .gitignore");

        appended.ShouldBeFalse();
        result.Changed.ShouldBeFalse();
        result.Skipped.ShouldBeFalse();
    }
}
