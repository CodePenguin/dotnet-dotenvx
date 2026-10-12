using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/normalizeDotenvConfigIgnore.test.js</remarks>
public class NormalizeDotenvConfigIgnoreTest : IDisposable
{
    public NormalizeDotenvConfigIgnoreTest()
    {
        Environment.SetEnvironmentVariable("DOTENV_IGNORE", null);
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_IGNORE", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTENV_IGNORE", null);
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_IGNORE", null);
    }

    /// <remarks>Refer to "returns options unchanged when DOTENV_CONFIG_IGNORE is unset"</remarks>
    [Fact]
    public void TestReturnsOptionsUnchangedWhenDotenvConfigIgnoreIsUnset()
    {
        ConfigOptions options = new() { Ignore = ["MISSING_KEY"] };

        NormalizeDotenvConfigIgnore(options).ShouldBeSameAs(options);
        options.Ignore.ShouldBe(["MISSING_KEY"]);
    }

    /// <remarks>Refer to "parses, trims, and filters comma-separated error codes"</remarks>
    [Fact]
    public void TestParsesTrimsAndFiltersCommaSeparatedErrorCodes()
    {
        Environment.SetEnvironmentVariable("DOTENV_IGNORE", " MISSING_ENV_FILE, MISSING_KEY, ");

        ConfigOptions result = NormalizeDotenvConfigIgnore(new ConfigOptions());

        result.Ignore.ShouldBe(["MISSING_ENV_FILE", "MISSING_KEY"]);
    }

    /// <remarks>Refer to "falls back to DOTENV_CONFIG_IGNORE"</remarks>
    [Fact]
    public void TestFallsBackToDotenvConfigIgnore()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_IGNORE", "MISSING_ENV_FILE");

        ConfigOptions result = NormalizeDotenvConfigIgnore(new ConfigOptions());

        result.Ignore.ShouldBe(["MISSING_ENV_FILE"]);
    }

    /// <remarks>Refer to "DOTENV_IGNORE takes precedence over DOTENV_CONFIG_IGNORE"</remarks>
    [Fact]
    public void TestDotenvIgnoreTakesPrecedenceOverDotenvConfigIgnore()
    {
        Environment.SetEnvironmentVariable("DOTENV_IGNORE", "MISSING_KEY");
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_IGNORE", "MISSING_ENV_FILE");

        ConfigOptions result = NormalizeDotenvConfigIgnore(new ConfigOptions());

        result.Ignore.ShouldBe(["MISSING_KEY"]);
    }

    /// <remarks>Refer to "merges without mutating explicit ignore options"</remarks>
    [Fact]
    public void TestMergesWithoutMutatingExplicitIgnoreOptions()
    {
        Environment.SetEnvironmentVariable("DOTENV_IGNORE", "MISSING_ENV_FILE");
        ConfigOptions options = new() { Ignore = ["MISSING_KEY"] };

        ConfigOptions result = NormalizeDotenvConfigIgnore(options);

        result.Ignore.ShouldBe(["MISSING_KEY", "MISSING_ENV_FILE"]);
        options.Ignore.ShouldBe(["MISSING_KEY"]);
        result.ShouldNotBeSameAs(options);
    }
}
