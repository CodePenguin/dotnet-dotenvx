using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/is-ignoring-dotenv-keys.test.js</remarks>
public class IsIgnoringDotenvKeysTest : EnvironmentTestBase
{
    [Fact]
    public void TestIsIgnoringDotenvKeysNoGitignoreFile()
    {
        Fsx.ExistsSync = _ => false;

        bool result = IsIgnoringDotenvKeys();
        result.ShouldBe(false);
    }

    [Fact]
    public void TestIsIgnoringDotenvKeysEmptyGitignoreFile()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";

        bool result = IsIgnoringDotenvKeys();
        result.ShouldBe(false);
    }

    [Fact]
    public void TestIsIgnoringDotenvKeysGitignoreFileIgnoresEnvStar()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => ".env*";

        bool result = IsIgnoringDotenvKeys();
        result.ShouldBe(true);
    }
}
