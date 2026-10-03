using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/normalizeDotenvConfigPath.test.js. Refer to "#normalizeDotenvConfigPath": the
/// single upstream test has several assertions; each becomes its own test below, named after the assertion message.
/// </remarks>
public class NormalizeDotenvConfigPathTest
{
    /// <remarks>Refer to "uses DOTENV_F when no env file was specified"</remarks>
    [Fact]
    public void TestUsesDotenvFWhenNoEnvFileWasSpecified()
    {
        EnvItem[] result = NormalizeDotenvConfigPath([], new Dictionary<string, string?> { ["DOTENV_F"] = ".env.production" });

        result.ShouldBeEquivalentTo(new[] { new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" } });
    }

    /// <remarks>Refer to "supports DOTENV_PATH and comma-separated env files"</remarks>
    [Fact]
    public void TestSupportsDotenvPathAndCommaSeparatedEnvFiles()
    {
        EnvItem[] result = NormalizeDotenvConfigPath([],
            new Dictionary<string, string?> { ["DOTENV_PATH"] = ".env.local, .env" });

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env" }
        });
    }

    /// <remarks>Refer to "DOTENV_PATH takes precedence over DOTENV_F"</remarks>
    [Fact]
    public void TestDotenvPathTakesPrecedenceOverDotenvF()
    {
        EnvItem[] result = NormalizeDotenvConfigPath([],
            new Dictionary<string, string?> { ["DOTENV_F"] = ".env.local", ["DOTENV_PATH"] = ".env.production" });

        result.ShouldBeEquivalentTo(new[] { new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" } });
    }

    /// <remarks>Refer to "explicit env files take precedence"</remarks>
    [Fact]
    public void TestExplicitEnvFilesTakePrecedence()
    {
        EnvItem[] explicitEnvs =
        [
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
            new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }
        ];

        EnvItem[] result = NormalizeDotenvConfigPath(explicitEnvs,
            new Dictionary<string, string?> { ["DOTENV_F"] = ".env.production" });

        result.ShouldBeEquivalentTo(explicitEnvs);
    }

    /// <remarks>Refer to "DOTENV_FILE takes precedence over older aliases and supports multiple files"</remarks>
    [Fact]
    public void TestDotenvFileTakesPrecedenceOverOlderAliasesAndSupportsMultipleFiles()
    {
        EnvItem[] result = NormalizeDotenvConfigPath([], new Dictionary<string, string?>
        {
            ["DOTENV_FILE"] = ".env.production, .env", ["DOTENV_PATH"] = ".env.old", ["DOTENV_F"] = ".env.older"
        });

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env" }
        });

        EnvItem[] explicitEnvs = [new EnvItem { Type = EnvType.EnvFile, Value = ".env.explicit" }];
        EnvItem[] explicitResult = NormalizeDotenvConfigPath(explicitEnvs,
            new Dictionary<string, string?> { ["DOTENV_FILE"] = ".env.ignored" });

        explicitResult.ShouldBeEquivalentTo(explicitEnvs);
    }

    /// <remarks>Refer to "leaves envs unchanged when unset"</remarks>
    [Fact]
    public void TestLeavesEnvsUnchangedWhenUnset()
    {
        EnvItem[] envs = [new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }];

        EnvItem[] result = NormalizeDotenvConfigPath(envs, new Dictionary<string, string?>());

        result.ShouldBeEquivalentTo(envs);
    }
}
