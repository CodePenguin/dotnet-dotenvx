using Dotenvx.Core.Interop.DotenvxJs.Types;

using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

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

    [Fact]
    public void Conventions_NextJs_ReturnsCorrectFiles()
    {
        List<EnvItem> envs = DotenvxHelpers.Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.development.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.development" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    [Fact]
    public void Conventions_Flow_ReturnsCorrectFiles()
    {
        List<EnvItem> envs = DotenvxHelpers.Conventions("flow");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.development.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.development" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" },
            new() { Type = EnvType.EnvFile, Value = ".env.defaults" }
        });
    }

    [Fact]
    public void Conventions_Invalid_ThrowsException()
    {
        Exception exception = Should.Throw<Exception>(() => DotenvxHelpers.Conventions("invalid"));

        exception.Message.ShouldBe("INVALID_CONVENTION: 'invalid'. permitted conventions: ['nextjs', 'flow']");
    }

    [Fact]
    public void Conventions_NextJs_WithNodeEnvTest_ReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "test");

        List<EnvItem> envs = DotenvxHelpers.Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.test.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.test" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    [Fact]
    public void Conventions_NextJs_WithDotenvEnvTest_ReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("DOTENV_ENV", "test");

        List<EnvItem> envs = DotenvxHelpers.Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.test.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.test" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    [Fact]
    public void Conventions_Flow_WithNodeEnvTest_ReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "test");

        List<EnvItem> envs = DotenvxHelpers.Conventions("flow");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.test.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.test" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" },
            new() { Type = EnvType.EnvFile, Value = ".env.defaults" }
        });
    }

    [Fact]
    public void Conventions_NextJs_WithUnrecognizedNodeEnv_ReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "unrecognized");

        List<EnvItem> envs = DotenvxHelpers.Conventions("nextjs");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    [Fact]
    public void Conventions_Flow_WithUnrecognizedNodeEnv_ReturnsCorrectFiles()
    {
        Environment.SetEnvironmentVariable("NODE_ENV", "unrecognized");

        List<EnvItem> envs = DotenvxHelpers.Conventions("flow");

        envs.ShouldBeEquivalentTo(new List<EnvItem>
        {
            new() { Type = EnvType.EnvFile, Value = ".env.unrecognized.local" },
            new() { Type = EnvType.EnvFile, Value = ".env.unrecognized" },
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" },
            new() { Type = EnvType.EnvFile, Value = ".env.defaults" }
        });
    }
}