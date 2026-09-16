using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/normalizeDotenvConfigQuiet.test.js</remarks>
public class NormalizeDotenvConfigQuietTest : IDisposable
{
    public NormalizeDotenvConfigQuietTest()
    {
        Environment.SetEnvironmentVariable("DOTENV_QUIET", null);
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_QUIET", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTENV_QUIET", null);
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_QUIET", null);
    }

    /// <remarks>Refer to "uses DOTENV_QUIET"</remarks>
    [Fact]
    public void TestUsesDotenvQuiet()
    {
        Environment.SetEnvironmentVariable("DOTENV_QUIET", "true");

        NormalizeDotenvConfigQuiet(new ConfigOptions()).Quiet.ShouldBe(true);
    }

    /// <remarks>Refer to "falls back to DOTENV_CONFIG_QUIET"</remarks>
    [Fact]
    public void TestFallsBackToDotenvConfigQuiet()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_QUIET", "true");

        NormalizeDotenvConfigQuiet(new ConfigOptions()).Quiet.ShouldBe(true);
    }

    /// <remarks>Refer to "DOTENV_QUIET takes precedence over DOTENV_CONFIG_QUIET"</remarks>
    [Fact]
    public void TestDotenvQuietTakesPrecedenceOverDotenvConfigQuiet()
    {
        Environment.SetEnvironmentVariable("DOTENV_QUIET", "false");
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_QUIET", "true");

        NormalizeDotenvConfigQuiet(new ConfigOptions()).Quiet.ShouldBeNull();
    }
}
