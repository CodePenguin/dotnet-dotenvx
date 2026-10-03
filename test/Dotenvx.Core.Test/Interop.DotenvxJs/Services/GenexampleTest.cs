using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/tests/lib/services/genexample.test.js. Uses Fsx swaps directly
/// (matching DelTest.cs's convention) rather than the real tests/monorepo/apps/frontend|backend
/// fixtures, since Genexample.Run() writes a .env.example file and this suite shouldn't mutate
/// the submodule's checked-out fixtures.
/// </remarks>
public class GenexampleTest : EnvironmentTestBase
{
    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRunFreshExampleFile()
    {
        Fsx.ExistsSync = path => path.Contains(".env") && !path.Contains(".env.example");
        Fsx.ReadFileX = (_, _) => "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\n";

        GenexampleResult result = new Genexample("apps/frontend", [".env"]).Run();

        result.Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "" } });
        result.Existed.ShouldBeEmpty();
        result.AddedKeys.ShouldBe(["HELLO"]);
        result.EnvExampleFile.ShouldBe(
            "# .env.example - generated with dotenvx\n\n# for testing purposes only\nHELLO=\"\" # this is a comment\n");
    }

    /// <remarks>Refer to "#run (.env.example already exists)"</remarks>
    [Fact]
    public void TestRunExampleFileAlreadyExists()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".env.example")
            ? "# .env.example - generated with dotenvx\nHELLO=''\n"
            : "# for testing purposes only\nHELLO=\"backend\"\n";

        GenexampleResult result = new Genexample("apps/backend", [".env"]).Run();

        result.Injected.ShouldBeEmpty();
        result.Existed.ShouldBe(new Dictionary<string, string> { { "HELLO", "" } });
        result.AddedKeys.ShouldBeEmpty();
        result.EnvExampleFile.ShouldBe("# .env.example - generated with dotenvx\nHELLO=''\n");
    }

    /// <remarks>Refer to "#run (.env.example already exists but with different keys)"</remarks>
    [Fact]
    public void TestRunExampleFileExistsWithDifferentKeys()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (path, _) => path.Contains(".env.example")
            ? "# .env.example - generated with dotenvx\nHELLO=''\n"
            : "HELLO=world\nHELLO2=universe";

        GenexampleResult result = new Genexample("apps/backend", [".env"]).Run();

        result.Injected.ShouldBe(new Dictionary<string, string> { { "HELLO2", "" } });
        result.Existed.ShouldBe(new Dictionary<string, string> { { "HELLO", "" } });
        result.AddedKeys.ShouldBe(["HELLO2"]);
        result.EnvExampleFile.ShouldBe("# .env.example - generated with dotenvx\nHELLO=''\nHELLO2=''\n");
    }

    /// <remarks>Refer to "#run (cant find directory)"</remarks>
    [Fact]
    public void TestRunCantFindDirectory()
    {
        Fsx.ReadDirSync = _ => throw new DirectoryNotFoundException();

        MissingDirectoryException error = Should.Throw<MissingDirectoryException>(() =>
            new Genexample("apps/frontendzzzz").Run());

        error.Message.ShouldBe("[MISSING_DIRECTORY] missing directory (apps/frontendzzzz)");
    }

    /// <remarks>Refer to "#run (missing env files)"</remarks>
    [Fact]
    public void TestRunMissingEnvFiles()
    {
        MissingEnvFilesException error = Should.Throw<MissingEnvFilesException>(() =>
            new Genexample("apps/frontend", []).Run());

        error.Code.ShouldBe("MISSING_ENV_FILES");
        error.Message.ShouldBe("[MISSING_ENV_FILES] no .env* files found");
        error.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/760]");
    }

    /// <remarks>
    /// Refer to "#run (non-existent .env file)". Confirmed live against the real Node CLI,
    /// 2026-09-24: genexample.js sets a custom error.help AFTER errors.js's missingEnvFile()
    /// factory already baked messageWithHelp using the generic help text, so the custom help
    /// text is dead code - real Node still prints the generic fix URL here, not the custom
    /// "? add it with [echo ...]" text this JS test file's own assertion (written against
    /// source reading alone, not live-verified) implies. Ported to match observed reality.
    /// </remarks>
    [Fact]
    public void TestRunNonExistentEnvFile()
    {
        Fsx.ExistsSync = _ => false;

        MissingEnvFileException error = Should.Throw<MissingEnvFileException>(() =>
            new Genexample("apps/frontend", [".env.nonexistent"]).Run());

        error.Code.ShouldBe("MISSING_ENV_FILE");
        error.MessageWithHelp.ShouldBe(
            "[MISSING_ENV_FILE] missing file (.env.nonexistent). fix: [https://github.com/dotenvx/dotenvx/issues/484]");
    }

    /// <remarks>Refer to "#run (string envFile)". The env file may be given as a single name (here a one-element list); the real fixture keeps its comment and blanks the value.</remarks>
    [Fact]
    public void TestRunStringEnvFile()
    {
        GenexampleResult result = new Genexample("tests/monorepo/apps/frontend", [".env"]).Run();

        result.EnvExampleFile.ShouldBe(
            "# .env.example - generated with dotenvx\n\n# for testing purposes only\nHELLO=\"\" # this is a comment\n");
        result.Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "" } });
        result.Existed.ShouldBeEmpty();
    }
}
