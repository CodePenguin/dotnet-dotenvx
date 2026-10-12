using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/findEnvFiles.test.js</remarks>
public class FindEnvFilesTest : EnvironmentTestBase
{
    /// <remarks>
    /// Refer to "#findEnvFiles". Confirms the reserved-filename and ".previous" exclusion filter
    /// (RESERVED_ENV_FILES = ['.env.project', '.env.keys', '.env.me', '.env.x', '.env.example']).
    /// </remarks>
    [Fact]
    public void TestFindEnvFilesFiltersReservedAndPreviousFiles()
    {
        Fsx.ReadDirSync = _ =>
        [
            ".env", ".env.production", ".env.production.previous", ".env.project", ".env.keys", ".env.me",
            ".env.x", ".env.example", "not-env-related.txt"
        ];

        string[] result = FindEnvFiles(".");

        result.ShouldBe([".env", ".env.production"]);
    }

    /// <remarks>Refer to "#findEnvFiles (tests/monorepo/apps/frontend)"</remarks>
    [Fact]
    public void TestFindEnvFilesSingleFile()
    {
        Fsx.ReadDirSync = _ => [".env"];

        FindEnvFiles("tests/monorepo/apps/frontend").ShouldBe([".env"]);
    }

    /// <remarks>Refer to "#findEnvFiles (bad directory)"</remarks>
    [Fact]
    public void TestFindEnvFilesBadDirectory()
    {
        Fsx.ReadDirSync = _ => throw new DirectoryNotFoundException();

        MissingDirectoryException error = Should.Throw<MissingDirectoryException>(() =>
            FindEnvFiles("tests/does/not/exist"));

        error.Code.ShouldBe("MISSING_DIRECTORY");
        error.Message.ShouldBe("[MISSING_DIRECTORY] missing directory (tests/does/not/exist)");
    }

    /// <remarks>Refer to "#findEnvFiles (other error)"</remarks>
    [Fact]
    public void TestFindEnvFilesOtherErrorPropagates()
    {
        Fsx.ReadDirSync = _ => throw new InvalidOperationException("Mock Error");

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => FindEnvFiles("."));

        error.Message.ShouldBe("Mock Error");
    }
}
