using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/prebuild.js</remarks>
public class PrebuildTest : EnvironmentTestBase
{
    private static void MockFilePaths(params string[] filepaths)
    {
        Ls mockLs = Substitute.For<Ls>();
        mockLs.Run().Returns(filepaths);
        Ls.Factory = (_, _, _) => mockLs;
    }

    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRunNoFiles()
    {
        MockFilePaths();
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";

        PrebuildResult result = new Prebuild().Run();

        result.SuccessMessage.ShouldBe("▣ no .env files");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no dockerignore file)"</remarks>
    [Fact]
    public void TestRunNoDockerignoreFile()
    {
        MockFilePaths();
        Fsx.ExistsSync = _ => false;

        PrebuildResult result = new Prebuild().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".dockerignore missing");
    }

    /// <remarks>Refer to "#run (dockerignore is ignoring .env.example file and shouldn't)"</remarks>
    [Fact]
    public void TestRunDockerignoreIgnoringExemptFileWarns()
    {
        MockFilePaths(".env.example");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";

        PrebuildResult result = new Prebuild().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".env.example ignored (should not be)");
    }

    /// <remarks>Refer to "#run (dockerignore is not ignore .env.production file and should)"</remarks>
    [Fact]
    public void TestRunUnencryptedNotIgnoredThrows()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".dockerignore") ? "" : "HELLO=world";

        CustomException error = Should.Throw<CustomException>(() => new Prebuild().Run());

        error.Message.ShouldBe(".env.production not encrypted/dockerignored");
        error.MessageWithHelp.ShouldBe(
            ".env.production not encrypted/dockerignored. fix: [dotenvx encrypt -f .env.production] or [dotenvx gitignore --pattern .env.production]");
    }

    /// <remarks>Refer to "#run (_PLAIN keys are safe when other values are encrypted)"</remarks>
    [Fact]
    public void TestRunPlainKeysAreSafe()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".dockerignore")
            ? ""
            : "DOTENV_PUBLIC_KEY=\"abc\"\nHELLO=\"encrypted:xyz\"\nSOMETHING_PLAIN=\"safe\"";

        PrebuildResult result = new Prebuild().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/dockerignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (dockerignore is not ignore .env.keys file and should)"</remarks>
    [Fact]
    public void TestRunUnignoredEnvKeysThrowsSpecialMessage()
    {
        MockFilePaths(".env.keys");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".dockerignore") ? "" : "DOTENV_PRIVATE_KEY=abc";

        CustomException error = Should.Throw<CustomException>(() => new Prebuild().Run());

        error.MessageWithHelp.ShouldBe(".env.keys not dockerignored. fix: [dotenvx gitignore --pattern .env.keys]");
    }

    /// <remarks>
    /// Refer to "#run (.env files in subfolders throw error in prebuild hook)". Confirms Prebuild's
    /// full-PATH exemption check (unlike Precommit's basename check, see PrecommitTest.cs) - a
    /// nested .env.example is NOT exempt here, verified live against the real Node CLI.
    /// </remarks>
    [Fact]
    public void TestRunNestedExemptFileIsNotExempt()
    {
        MockFilePaths(Path.Combine("apps", "app1", ".env.example"));
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".dockerignore") ? "" : "HELLO=world";

        CustomException error = Should.Throw<CustomException>(() => new Prebuild().Run());

        error.Message.ShouldContain("not encrypted/dockerignored");
    }

    /// <remarks>Refer to "#run (dockerignore is ignoring .env.x file and shouldn't)"</remarks>
    [Fact]
    public void TestRunDockerignoreIgnoringEnvXWarns()
    {
        MockFilePaths(".env.x");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";

        PrebuildResult result = new Prebuild().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".env.x ignored (should not be)");
    }

    /// <remarks>Refer to "#run (dockerignore is ignoring .env.vault file and shouldn't)"</remarks>
    [Fact]
    public void TestRunDockerignoreIgnoringEnvVaultWarns()
    {
        MockFilePaths(".env.vault");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";

        PrebuildResult result = new Prebuild().Run();

        result.Warnings.ShouldHaveSingleItem();
        result.Warnings[0].Message.ShouldBe(".env.vault ignored (should not be)");
    }

    /// <remarks>Refer to "#run (.env.vault is exempt and does not throw)"</remarks>
    [Fact]
    public void TestRunEnvVaultIsExemptAndDoesNotThrow()
    {
        MockFilePaths(".env.vault");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == ".env.vault" ? "DOTENV_VAULT_PRODUCTION=\"ciphertext\"" : "";

        PrebuildResult result = new Prebuild().Run();

        result.SuccessMessage.ShouldBe("▣ encrypted/dockerignored (1)");
        result.Warnings.ShouldBeEmpty();
    }

    /// <remarks>
    /// Refer to "#run (dockerignore is not ignore .env.production file and should) AND isFileToBeCommitted raises an error (should default to true on the filename)".
    /// Prebuild has no git check at all, so the upstream test's failing `execSync` stub is irrelevant here; the file
    /// is simply unencrypted and not dockerignored.
    /// </remarks>
    [Fact]
    public void TestRunUnencryptedNotIgnoredThrowsEvenWhenAGitCheckWouldFail()
    {
        MockFilePaths(".env.production");
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path == ".env" ? ".env" : path == ".env.production" ? "ENV_VAR=value" : "";

        CustomException error = Should.Throw<CustomException>(() => new Prebuild().Run());

        error.Message.ShouldBe(".env.production not encrypted/dockerignored");
    }
}
