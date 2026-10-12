using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/precommit.js. Every test here forces
/// `GitHelper.IsInsideGitWorkTree = () =&gt; false` unless it's specifically testing git-awareness,
/// so the "is this file actually staged/changed" gate always defaults to "yes, check it" without
/// depending on this repo's OWN real git state (which test-runner CWD/working-tree changes could
/// otherwise make flaky) or spawning a real git process.
/// </remarks>
public class PrecommitTest : EnvironmentTestBase
{
    private static void MockFilePaths(params string[] filepaths)
    {
        Ls mockLs = Substitute.For<Ls>();
        mockLs.Run().Returns(filepaths);
        Ls.Factory = (_, _, _) => mockLs;
    }

    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRunDefaultNoInstall()
    {
        MockFilePaths();
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";

        PrecommitResult result = new Precommit().Run();

        result.SuccessMessage.ShouldBe("▣ no .env files");
    }

    /// <remarks>Refer to "#run (install: true)"</remarks>
    [Fact]
    public void TestRunInstallTrueCallsInstallHook()
    {
        Fsx.ExistsSync = _ => false;
        Fsx.WriteFileX = (_, _) => { };
        Fsx.ChmodSync = (_, _) => { };

        PrecommitResult result = new Precommit(install: true).Run();

        result.SuccessMessage.ShouldStartWith("▣ dotenvx precommit installed");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no gitignore file)"</remarks>
    [Fact]
    public void TestRunNoGitignoreFile()
    {
        MockFilePaths();
        Fsx.ExistsSync = _ => false;
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".gitignore missing");
    }

    /// <remarks>Refer to "#run (gitignore is ignoring .env.example file and shouldn't)"</remarks>
    [Fact]
    public void TestRunGitignoreIgnoringExemptFileWarns()
    {
        MockFilePaths(".env.example");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".env.example ignored (should not be)");
    }

    /// <remarks>Refer to "#run (gitignore is not ignore .env.production file and should)"</remarks>
    [Fact]
    public void TestRunUnencryptedNotIgnoredThrows()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "HELLO=world";
        GitHelper.IsInsideGitWorkTree = () => false;

        CustomException error = Should.Throw<CustomException>(() => new Precommit().Run());

        error.MessageWithHelp.ShouldBe(
            ".env.production not encrypted/gitignored. fix: [dotenvx encrypt -f .env.production] or [dotenvx gitignore --pattern .env.production]");
    }

    /// <remarks>Refer to "#run (_PLAIN keys are safe when other values are encrypted)"</remarks>
    [Fact]
    public void TestRunPlainKeysAreSafe()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore")
            ? ""
            : "DOTENV_PUBLIC_KEY=\"abc\"\nHELLO=\"encrypted:xyz\"\nSOMETHING_PLAIN=\"safe\"";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/gitignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (gitignore is not ignore .env.keys file and should)"</remarks>
    [Fact]
    public void TestRunUnignoredEnvKeysThrowsSpecialMessage()
    {
        MockFilePaths(".env.keys");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "DOTENV_PRIVATE_KEY=abc";
        GitHelper.IsInsideGitWorkTree = () => false;

        CustomException error = Should.Throw<CustomException>(() => new Precommit().Run());

        error.MessageWithHelp.ShouldBe(".env.keys not gitignored. fix: [dotenvx gitignore --pattern .env.keys]");
    }

    /// <remarks>
    /// Refer to "#run (isFileToBeCommitted raises an error (should default to true on the
    /// filename))" - GitHelper reports being inside a repo, but the diff call itself fails.
    /// </remarks>
    [Fact]
    public void TestRunGitDiffFailureDefaultsToAlwaysCheck()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "HELLO=world";
        GitHelper.IsInsideGitWorkTree = () => true;
        GitHelper.DiffNameOnlyNulSeparated = () => throw new InvalidOperationException("git diff failed");

        Should.Throw<CustomException>(() => new Precommit().Run());
    }

    /// <remarks>
    /// Refer to "#run (isInGitRepo raises an error (should default to true on the filename))".
    /// </remarks>
    [Fact]
    public void TestRunNotAGitRepoDefaultsToAlwaysCheck()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "HELLO=world";
        GitHelper.IsInsideGitWorkTree = () => false;

        Should.Throw<CustomException>(() => new Precommit().Run());
    }

    /// <remarks>
    /// Refer to "#run (.env file at a path git c-quotes is still checked)" - confirms NUL-separated
    /// (not line-separated) git diff parsing correctly matches a non-ASCII path that git would
    /// otherwise C-quote/octal-escape in plain output. Deterministic via the GitHelper seam, not a
    /// real repo with a non-ASCII filename on disk. Uses a top-level (non-nested) café filename so
    /// NodePathJoin's platform-separator normalization doesn't affect the comparison - verified
    /// live against the real Node CLI that a NESTED staged file's backslash-joined path (Windows'
    /// `path.join`) never matches git diff's always-forward-slash output there, an upstream
    /// cross-platform quirk this port faithfully reproduces rather than "fixing" (see
    /// TestRunNonExemptSubfolderFileThrows's git-unaware variant for that nested case instead).
    /// </remarks>
    [Fact]
    public void TestRunNonAsciiPathStillChecked()
    {
        const string cafePath = "café.env.production";
        MockFilePaths(cafePath);
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "HELLO=world";
        GitHelper.IsInsideGitWorkTree = () => true;
        GitHelper.DiffNameOnlyNulSeparated = () => [cafePath];

        CustomException error = Should.Throw<CustomException>(() => new Precommit().Run());

        error.Message.ShouldContain(cafePath);
    }

    /// <remarks>Refer to "#run (.env files in subfolders throw error in precommit hook)"</remarks>
    [Fact]
    public void TestRunNonExemptSubfolderFileThrows()
    {
        MockFilePaths(Path.Combine("packages", "app", ".env.production"));
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "HELLO=world";
        GitHelper.IsInsideGitWorkTree = () => false;

        CustomException error = Should.Throw<CustomException>(() => new Precommit().Run());

        error.Message.ShouldContain("not encrypted/gitignored");
    }

    /// <remarks>
    /// Refer to "#run (.env.example in a subfolder is exempt and does not throw)". Confirms
    /// Precommit's BASENAME exemption check (unlike Prebuild's full-path check, see
    /// PrebuildTest.cs) - a nested .env.example IS exempt here, verified live against Node.
    /// </remarks>
    [Fact]
    public void TestRunNestedExemptFileIsExempt()
    {
        MockFilePaths(Path.Combine("app", ".env.example"));
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".gitignore") ? "" : "FOOBAR=\"baz\"";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/gitignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "_installPrecommitHook calls InstallPrecommitHook.run"</remarks>
    [Fact]
    public void TestInstallShortCircuitsWarnings()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "dotenvx precommit";

        PrecommitResult result = new Precommit(install: true).Run();

        result.SuccessMessage.ShouldStartWith("▣ dotenvx precommit exists");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (gitignore is ignoring .env.x file and shouldn't)"</remarks>
    [Fact]
    public void TestRunGitignoreIgnoringEnvXWarns()
    {
        MockFilePaths(".env.x");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".env.x ignored (should not be)");
    }

    /// <remarks>Refer to "#run (gitignore is ignoring .env.vault file and shouldn't)"</remarks>
    [Fact]
    public void TestRunGitignoreIgnoringEnvVaultWarns()
    {
        MockFilePaths(".env.vault");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".env.vault ignored (should not be)");
    }

    /// <remarks>
    /// Refer to "#run (gitignore is not ignore .env.production file and should) AND isFileToBeCommited raises an error (should default to true on the filename)":
    /// inside a repo, but the staged-files query fails.
    /// </remarks>
    [Fact]
    public void TestRunStagedFilesQueryFailureDefaultsToAlwaysCheck()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == ".env" ? ".env" : path == ".env.production" ? "ENV_VAR=value" : "";
        GitHelper.IsInsideGitWorkTree = () => true;
        GitHelper.DiffNameOnlyNulSeparated = () => throw new Exception("Mock Error");

        CustomException error = Should.Throw<CustomException>(() => new Precommit().Run());

        error.Message.ShouldBe(".env.production not encrypted/gitignored");
    }

    /// <remarks>
    /// Refer to "#run (gitignore is not ignore .env.production file and should) AND isInGitRepo raises an error (should default to true on the filename)":
    /// the repo check itself fails.
    /// </remarks>
    [Fact]
    public void TestRunRepoCheckFailureDefaultsToAlwaysCheck()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == ".env" ? ".env" : path == ".env.production" ? "ENV_VAR=value" : "";
        GitHelper.IsInsideGitWorkTree = () => throw new Exception("Mock Error");

        CustomException error = Should.Throw<CustomException>(() => new Precommit().Run());

        error.Message.ShouldBe(".env.production not encrypted/gitignored");
    }

    /// <remarks>Refer to "#run (.env.x in a subfolder is exempt and does not throw)"</remarks>
    [Fact]
    public void TestRunEnvXInASubfolderIsExempt()
    {
        string file = Path.Combine("app", ".env.x");
        MockFilePaths(file);
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == file ? "FOOBAR=\"baz\"" : "";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/gitignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (.env.vault is exempt and does not throw)"</remarks>
    [Fact]
    public void TestRunEnvVaultIsExempt()
    {
        MockFilePaths(".env.vault");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == ".env.vault" ? "DOTENV_VAULT_PRODUCTION=\"ciphertext\"" : "";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/gitignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (.env.vault in a subfolder is exempt and does not throw)"</remarks>
    [Fact]
    public void TestRunEnvVaultInASubfolderIsExempt()
    {
        string file = Path.Combine("app", ".env.vault");
        MockFilePaths(file);
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == file ? "DOTENV_VAULT_PRODUCTION=\"ciphertext\"" : "";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/gitignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (gitignore is ignoring a subfolder .env.example file and shouldn't)"</remarks>
    [Fact]
    public void TestRunGitignoreIgnoringASubfolderEnvExampleWarns()
    {
        string file = Path.Combine("app", ".env.example");
        MockFilePaths(file);
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";
        GitHelper.IsInsideGitWorkTree = () => false;

        PrecommitResult result = new Precommit().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe($"{file} ignored (should not be)");
    }
}
