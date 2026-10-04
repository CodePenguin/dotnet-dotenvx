using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/resolveEnvKeysFile.js. No dedicated upstream unit-test
/// file exists for this helper at this pin - scenarios mirror the function's own two branches
/// (Array.isArray / scalar-or-falsy) directly rather than a ported JS test title.
/// </remarks>
public class ResolveEnvKeysFileTest : EnvironmentTestBase
{
    [Fact]
    public void TestEveryEntryIsResolvedWithEnvKeysFilename()
    {
        Fsx.IsDirectorySync = p => p == "./config" || p == "./other";

        string[]? result = ResolveEnvKeysFile(["./config", "./other", ".env.production.keys"]);

        result.ShouldBe([
            NodePathJoin("./config", ".env.keys"),
            NodePathJoin("./other", ".env.keys"),
            ".env.production.keys"
        ]);
    }

    [Fact]
    public void TestMissingPathIsKeptUnchanged()
    {
        // upstream's resolveDirectoryFilepath swallows fs.statSync's ENOENT, so a directory that does
        // not exist survives verbatim into the list and is only reported later by keypair/decrypt.
        Fsx.IsDirectorySync = _ => false;

        string[]? result = ResolveEnvKeysFile(["./does-not-exist"]);

        result.ShouldBe(["./does-not-exist"]);
    }

    [Fact]
    public void TestNullStaysNull()
    {
        // `return envKeysFile && ...` - a null/undefined envKeysFile short-circuits to itself, and no
        // default '.env.keys' is invented here. `resolveDirectoryFilepath` is never reached.
        string[]? result = ResolveEnvKeysFile(null);

        result.ShouldBeNull();
    }

    [Fact]
    public void TestEmptyListStaysEmpty()
    {
        Fsx.IsDirectorySync = _ => throw new ShouldAssertException("IsDirectorySync must not be called");

        string[]? result = ResolveEnvKeysFile([]);

        result.ShouldBeEmpty();
    }
}