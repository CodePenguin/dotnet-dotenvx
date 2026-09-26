using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/cli/dotenvx.js's `collectEnvs('envFile')`. No dedicated upstream
/// unit-test file exists for this - it's an inline closure inside the CLI's own commander setup,
/// not something with its own JS test - scenarios mirror `value.split(',').map(item =>
/// item.trim()).filter(Boolean)` directly.
/// </remarks>
public class SplitCommaSeparatedEnvFilesTest
{
    [Fact]
    public void TestNullReturnsEmpty()
    {
        SplitCommaSeparatedEnvFiles(null).ShouldBeEmpty();
    }

    [Fact]
    public void TestSingleFileUnchanged()
    {
        SplitCommaSeparatedEnvFiles([".env"]).ShouldBe([".env"]);
    }

    [Fact]
    public void TestCommaSeparatedSingleTokenSplitsIntoMultiple()
    {
        SplitCommaSeparatedEnvFiles([".env,.env.production"]).ShouldBe([".env", ".env.production"]);
    }

    [Fact]
    public void TestTrimsWhitespaceAroundCommas()
    {
        SplitCommaSeparatedEnvFiles([".env, .env.production , .env.local"])
            .ShouldBe([".env", ".env.production", ".env.local"]);
    }

    [Fact]
    public void TestFiltersEmptySegments()
    {
        SplitCommaSeparatedEnvFiles([".env,,.env.production"]).ShouldBe([".env", ".env.production"]);
    }

    [Fact]
    public void TestMultipleTokensEachCommaSplit()
    {
        SplitCommaSeparatedEnvFiles([".env,.env.local", ".env.production"])
            .ShouldBe([".env", ".env.local", ".env.production"]);
    }
}
