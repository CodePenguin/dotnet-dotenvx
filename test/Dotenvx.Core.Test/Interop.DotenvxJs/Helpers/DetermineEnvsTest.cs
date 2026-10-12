using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/envResolution/determine.js. No dedicated upstream
/// unit-test file exists for this helper at this pin - scenarios mirror the function's own
/// branches directly rather than a ported JS test title.
/// </remarks>
public class DetermineEnvsTest
{
    [Fact]
    public void TestNoEnvsAndNoPrivateKeyReturnsDefaultDotEnv()
    {
        EnvItem[] result = DetermineEnvs([], new Dictionary<string, string>());

        result.ShouldBeEquivalentTo(new[] { new EnvItem { Type = EnvType.EnvFile, Value = ".env" } });
    }

    [Fact]
    public void TestNoEnvsWithDotenvPrivateKeySetReturnsGuessedFilenameInsteadOfDotEnv()
    {
        EnvItem[] result = DetermineEnvs([],
            new Dictionary<string, string> { ["DOTENV_PRIVATE_KEY_PRODUCTION"] = "deadbeef" });

        result.ShouldBeEquivalentTo(new[] { new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" } });
    }

    [Fact]
    public void TestFileAlreadySpecifiedReturnsEnvsUnchangedEvenWithPrivateKeySet()
    {
        EnvItem[] envs = [new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" }];

        EnvItem[] result = DetermineEnvs(envs,
            new Dictionary<string, string> { ["DOTENV_PRIVATE_KEY"] = "deadbeef" });

        result.ShouldBeEquivalentTo(envs);
    }

    [Fact]
    public void TestEnvStringsOnlyWithNoPrivateKeyPrependsDefaultDotEnv()
    {
        EnvItem[] envs = [new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }];

        EnvItem[] result = DetermineEnvs(envs, new Dictionary<string, string>());

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env" },
            new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }
        });
    }

    /// <remarks>
    /// Regression test for the 2026-09-24 fix: this branch previously always prepended the
    /// hardcoded ".env" here, ignoring DOTENV_PRIVATE_KEY* whenever the caller passed only --env
    /// strings with no -f - upstream's `defaults` (private-key-derived filenames when set) is
    /// computed once and reused in both the no-envs-at-all branch and this one.
    /// </remarks>
    [Fact]
    public void TestEnvStringsOnlyWithDotenvPrivateKeySetPrependsGuessedFilenameNotDotEnv()
    {
        EnvItem[] envs = [new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }];

        EnvItem[] result = DetermineEnvs(envs,
            new Dictionary<string, string> { ["DOTENV_PRIVATE_KEY_PRODUCTION"] = "deadbeef" });

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" },
            new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }
        });
    }

    [Fact]
    public void TestMultiplePrivateKeyNamesEachContributeAGuessedFilename()
    {
        EnvItem[] result = DetermineEnvs([], new Dictionary<string, string>
        {
            ["DOTENV_PRIVATE_KEY"] = "deadbeef", ["DOTENV_PRIVATE_KEY_PRODUCTION"] = "beefdead"
        });

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" }
        });
    }
}
