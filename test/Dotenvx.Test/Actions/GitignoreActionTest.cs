using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js</remarks>
public class GitignoreActionTest : EnvironmentTestBase
{
    public GitignoreActionTest()
    {
        RootCommand = new LoggingRootCommand { new GitignoreAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    private static GitignoreGenericResult Changed(string filename, params string[] patterns)
    {
        return new GitignoreGenericResult
        {
            Filename = filename, Patterns = patterns, Changed = true, Skipped = false,
            VerboseMessage = $"add to {filename}"
        };
    }

    private static GitignoreGenericResult NoChange(string filename, params string[] patterns)
    {
        return new GitignoreGenericResult
        {
            Filename = filename, Patterns = patterns, Changed = false, Skipped = false,
            VerboseMessage = $"add to {filename}"
        };
    }

    private static GitignoreGenericResult Skipped(string filename)
    {
        return new GitignoreGenericResult
        {
            Filename = filename, Patterns = [], Changed = false, Skipped = true,
            VerboseMessage = $"add to {filename}"
        };
    }

    /// <remarks>Refer to "gitignore calls Git, Docker, Npm, and Vercel"</remarks>
    [Fact]
    public void TestGitignoreDefaultsPatternWhenOmitted()
    {
        Gitignore mockGitignore = Substitute.For<Gitignore>();
        mockGitignore.Run().Returns(
        [
            Changed(".gitignore", ".env*"), Changed(".dockerignore", ".env*"), Skipped(".npmignore"),
            Skipped(".vercelignore")
        ]);
        string[]? capturedPatterns = null;
        Gitignore.Factory = patterns =>
        {
            capturedPatterns = patterns;
            return mockGitignore;
        };

        int exitCode = RootCommand.Parse(["gitignore"]).Invoke();

        exitCode.ShouldBe(0);
        capturedPatterns.ShouldBe([".env*"]);
        mockGitignore.Received(1).Run();
        MockLogger.InfoMessages.ShouldContain("▣ ignored .env* (.gitignore)");
        MockLogger.InfoMessages.ShouldContain("▣ ignored .env* (.dockerignore)");
    }

    [Fact]
    public void TestGitignorePassesExplicitPatterns()
    {
        Gitignore mockGitignore = Substitute.For<Gitignore>();
        mockGitignore.Run().Returns([Changed(".gitignore", ".env.keys", "foo")]);
        string[]? capturedPatterns = null;
        Gitignore.Factory = patterns =>
        {
            capturedPatterns = patterns;
            return mockGitignore;
        };

        int exitCode = RootCommand.Parse(["gitignore", "--pattern", ".env.keys", "--pattern", "foo"]).Invoke();

        exitCode.ShouldBe(0);
        capturedPatterns.ShouldBe([".env.keys", "foo"]);
        MockLogger.InfoMessages.ShouldContain("▣ ignored .env.keys,foo (.gitignore)");
    }

    [Fact]
    public void TestGitignoreNoChangeLogsInfoLine()
    {
        Gitignore mockGitignore = Substitute.For<Gitignore>();
        mockGitignore.Run().Returns([NoChange(".gitignore", ".env*")]);
        Gitignore.Factory = _ => mockGitignore;

        int exitCode = RootCommand.Parse(["gitignore"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("○ no change (.gitignore)");
    }

    /// <remarks>
    /// Not from a direct JS test - verifies the Skipped result (file absent, not touched, e.g.
    /// .npmignore when missing) produces NO log line at all, matching gitignore.js's Generic.run()
    /// returning early with no logger call in that branch (verified live against the real Node CLI).
    /// </remarks>
    [Fact]
    public void TestGitignoreSkippedFileLogsNothing()
    {
        Gitignore mockGitignore = Substitute.For<Gitignore>();
        mockGitignore.Run().Returns([Skipped(".npmignore")]);
        Gitignore.Factory = _ => mockGitignore;

        int exitCode = RootCommand.Parse(["gitignore"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldNotContain(s => s.Contains(".npmignore"));
        MockLogger.ErrorMessages.ShouldBeEmpty();
        MockLogger.VerboseMessages.ShouldContain("add to .npmignore");
    }
}
