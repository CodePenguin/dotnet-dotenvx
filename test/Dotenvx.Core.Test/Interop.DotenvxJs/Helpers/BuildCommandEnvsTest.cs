using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/buildCommandEnvs.js. No dedicated upstream unit-test
/// file exists for this helper at this pin - scenarios mirror the function's own branches directly
/// (directory resolution via resolveDirectoryFilepath, per-convention expansion under a resolved
/// directory, and hasDirectory-gated convention prepending) rather than a ported JS test title.
/// </remarks>
public class BuildCommandEnvsTest : EnvironmentTestBase
{
    public BuildCommandEnvsTest()
    {
        // Conventions() reads these; clear so "flow"/"nextjs" scenarios below use its "development"
        // default deterministically, matching ConventionsTest.cs's own approach.
        Environment.SetEnvironmentVariable("DOTENV_ENV", null);
        Environment.SetEnvironmentVariable("NODE_ENV", null);
    }

    [Fact]
    public void TestNonEnvFileItemsPassThroughUnchanged()
    {
        EnvItem[] envs = [new EnvItem { Type = EnvType.Env, Value = "HELLO=World" }];

        EnvItem[] result = BuildCommandEnvs(envs, null);

        result.ShouldBeEquivalentTo(envs);
    }

    [Fact]
    public void TestOrdinaryFileWithNoConventionPassesThroughUnchanged()
    {
        Fsx.IsDirectorySync = _ => false;
        EnvItem[] envs = [new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" }];

        EnvItem[] result = BuildCommandEnvs(envs, null);

        result.ShouldBeEquivalentTo(envs);
    }

    [Fact]
    public void TestOrdinaryFileWithConventionPrependsConventionsUnchanged()
    {
        Fsx.IsDirectorySync = _ => false;
        EnvItem[] envs = [new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" }];

        EnvItem[] result = BuildCommandEnvs(envs, "flow");

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.development.local" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.development" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.defaults" },
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.production" }
        });
    }

    [Fact]
    public void TestDirectoryWithNoConventionResolvesToDotEnvInsideIt()
    {
        Fsx.IsDirectorySync = p => p == "./config";
        EnvItem[] envs = [new EnvItem { Type = EnvType.EnvFile, Value = "./config" }];

        EnvItem[] result = BuildCommandEnvs(envs, null);

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env") }
        });
    }

    [Fact]
    public void TestDirectoryWithConventionExpandsToEachConventionFileJoinedUnderIt()
    {
        Fsx.IsDirectorySync = p => p == "./config";
        EnvItem[] envs = [new EnvItem { Type = EnvType.EnvFile, Value = "./config" }];

        EnvItem[] result = BuildCommandEnvs(envs, "flow");

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env.development.local") },
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env.development") },
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env.local") },
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env") },
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env.defaults") }
        });
    }

    [Fact]
    public void TestMixOfDirectoryAndOrdinaryFilePreservesOrderAndOnlyExpandsTheDirectory()
    {
        Fsx.IsDirectorySync = p => p == "./config";
        EnvItem[] envs =
        [
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
            new EnvItem { Type = EnvType.EnvFile, Value = "./config" }
        ];

        EnvItem[] result = BuildCommandEnvs(envs, null);

        result.ShouldBeEquivalentTo(new[]
        {
            new EnvItem { Type = EnvType.EnvFile, Value = ".env.local" },
            new EnvItem { Type = EnvType.EnvFile, Value = NodePathJoin("./config", ".env") }
        });
    }
}
