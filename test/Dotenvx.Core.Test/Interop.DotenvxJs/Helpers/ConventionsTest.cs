using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/conventions.test.js</remarks>
public class ConventionsTest : IDisposable
{
    public ConventionsTest()
    {
        // Clear environment variables before each test
        Environment.SetEnvironmentVariable("DOTENV_ENV", null);
        Environment.SetEnvironmentVariable("NODE_ENV", null);
    }

    public void Dispose()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable("DOTENV_ENV", null);
        Environment.SetEnvironmentVariable("NODE_ENV", null);
    }

    /// <remarks>Refer to "#conventions"</remarks>
    [Fact]
    public void TestConventionsNextJsReturnsCorrectFiles()
    {
        EnvItem[] envs = Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.development.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.development" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    /// <remarks>Refer to "#conventions flow"</remarks>
    [Fact]
    public void TestConventionsFlowReturnsCorrectFiles()
    {
        EnvItem[] envs = Conventions("flow");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.development.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.development" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" },
            new() { Type = EnvType.EnvFile, Value = ".env.defaults" }
        });
    }

    /// <remarks>Refer to "#conventions (invalid)"</remarks>
    [Fact]
    public void TestConventionsInvalidThrowsException()
    {
        Exception exception = Should.Throw<Exception>(() => Conventions("invalid"));

        exception.Message.ShouldBe("INVALID_CONVENTION: 'invalid'. permitted conventions: ['nextjs', 'flow']");
    }

    /// <remarks>Refer to "#conventions (process.env.NODE_ENV is test)"</remarks>
    [Fact]
    public void TestConventionsNextJsWithNodeEnvTestReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "test");

        EnvItem[] envs = Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.test.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.test" }, new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    /// <remarks>Refer to "#conventions (process.env.DOTENV_ENV is test)"</remarks>
    [Fact]
    public void TestConventionsNextJsWithDotenvEnvTestReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("DOTENV_ENV", "test");

        EnvItem[] envs = Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.test.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.test" }, new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    /// <remarks>Refer to "#conventions flow (process.env.NODE_ENV is test)"</remarks>
    [Fact]
    public void TestConventionsFlowWithNodeEnvTestReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "test");

        EnvItem[] envs = Conventions("flow");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.test.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.test" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" },
            new() { Type = EnvType.EnvFile, Value = ".env.defaults" }
        });
    }

    /// <remarks>Refer to "#conventions (process.env.NODE_ENV is unrecognized)"</remarks>
    [Fact]
    public void TestConventionsNextJsWithUnrecognizedNodeEnvReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "unrecognized");

        EnvItem[] envs = Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.local" }, new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    /// <remarks>Refer to "#conventions flow (process.env.NODE_ENV is unrecognized)"</remarks>
    [Fact]
    public void TestConventionsFlowWithUnrecognizedNodeEnvReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "unrecognized");

        EnvItem[] envs = Conventions("flow");

        envs.ShouldBeEquivalentTo(new EnvItem[]
        {
            new() { Type = EnvType.EnvFile, Value = ".env.unrecognized.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.unrecognized" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" },
            new() { Type = EnvType.EnvFile, Value = ".env.defaults" }
        });
    }
}