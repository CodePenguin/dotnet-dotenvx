using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/is-ignoring-dotenv-keys.test.js</remarks>
public class IsIgnoringDotenvKeysTest : EnvironmentTestBase
{
    protected override void TearDown()
    {
        File.Delete(".gitignore");
    }

    [Fact]
    public void TestIsIgnoringDotenvKeysNoGitignoreFile()
    {
        bool result = IsIgnoringDotenvKeys();
        result.ShouldBe(false);
    }

    [Fact]
    public void TestIsIgnoringDotenvKeysEmptyGitignoreFile()
    {
        File.WriteAllText(".gitignore", "");

        bool result = IsIgnoringDotenvKeys();
        result.ShouldBe(false);
    }

    [Fact]
    public void TestIsIgnoringDotenvKeysGitignoreFileIgnoresEnvStar()
    {
        File.WriteAllText(".gitignore", ".env*");

        bool result = IsIgnoringDotenvKeys();
        result.ShouldBe(true);
    }
}