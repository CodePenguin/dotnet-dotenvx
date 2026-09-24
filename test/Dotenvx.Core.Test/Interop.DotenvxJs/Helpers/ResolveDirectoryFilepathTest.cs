using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/resolveDirectoryFilepath.js. No dedicated upstream
/// unit-test file exists for this helper at this pin - scenarios mirror the function's own two
/// branches directly rather than a ported JS test title.
/// </remarks>
public class ResolveDirectoryFilepathTest : EnvironmentTestBase
{
    [Fact]
    public void TestDirectoryJoinsWithTheGivenFilename()
    {
        Fsx.IsDirectorySync = p => p == "./config";

        string result = ResolveDirectoryFilepath("./config", ".env");

        result.ShouldBe(NodePathJoin("./config", ".env"));
    }

    [Fact]
    public void TestOrdinaryFileIsReturnedUnchanged()
    {
        Fsx.IsDirectorySync = _ => false;

        string result = ResolveDirectoryFilepath(".env.production", ".env");

        result.ShouldBe(".env.production");
    }

    [Fact]
    public void TestMissingPathIsReturnedUnchanged()
    {
        // fs.statSync throws ENOENT for a missing path, caught and swallowed upstream - Directory
        // .Exists already returns false for a missing path with no exception, same net effect.
        Fsx.IsDirectorySync = _ => false;

        string result = ResolveDirectoryFilepath("./does-not-exist", ".env");

        result.ShouldBe("./does-not-exist");
    }
}
