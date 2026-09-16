using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/normalizeDotenvConfigConvention.test.js</remarks>
public class NormalizeDotenvConfigConventionTest : IDisposable
{
    public NormalizeDotenvConfigConventionTest()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONVENTION", null);
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_CONVENTION", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONVENTION", null);
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_CONVENTION", null);
    }

    /// <remarks>Refer to "uses DOTENV_CONVENTION"</remarks>
    [Fact]
    public void TestUsesDotenvConvention()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONVENTION", "flow");

        NormalizeDotenvConfigConvention(new ConfigOptions()).Convention.ShouldBe("flow");
    }

    /// <remarks>Refer to "falls back to DOTENV_CONFIG_CONVENTION"</remarks>
    [Fact]
    public void TestFallsBackToDotenvConfigConvention()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_CONVENTION", "nextjs");

        NormalizeDotenvConfigConvention(new ConfigOptions()).Convention.ShouldBe("nextjs");
    }

    /// <remarks>Refer to "DOTENV_CONVENTION takes precedence over DOTENV_CONFIG_CONVENTION"</remarks>
    [Fact]
    public void TestDotenvConventionTakesPrecedenceOverDotenvConfigConvention()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONVENTION", "flow");
        Environment.SetEnvironmentVariable("DOTENV_CONFIG_CONVENTION", "nextjs");

        NormalizeDotenvConfigConvention(new ConfigOptions()).Convention.ShouldBe("flow");
    }

    /// <remarks>Refer to "explicit convention takes precedence"</remarks>
    [Fact]
    public void TestExplicitConventionTakesPrecedence()
    {
        Environment.SetEnvironmentVariable("DOTENV_CONVENTION", "flow");

        NormalizeDotenvConfigConvention(new ConfigOptions { Convention = "nextjs" }).Convention.ShouldBe("nextjs");
    }
}
