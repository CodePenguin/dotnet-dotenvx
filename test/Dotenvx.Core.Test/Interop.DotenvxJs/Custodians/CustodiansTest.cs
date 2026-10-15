using Dotenvx.Interop.DotenvxJs.Custodians;
using CustodiansRegistry = global::Dotenvx.Interop.DotenvxJs.Custodians.Custodians;
using Dotenvx.Interop.DotenvxJs.Helpers;

using NSubstitute;

using Shouldly;

using NativeStoreException = global::Dotenvx.Providers.NativeStoreException;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Custodians;

/// <remarks>
/// Reference source: dotenvx/tests/lib/custodians/{index,native}.test.js. Upstream builds registries from plugin objects
/// (`createRegistry`) and proxyquires the native backend; this port's registry is the fixed built-in set, so the scenarios that
/// register custom plugins have no counterpart while the fallback and failure rules run
/// against a substitute OS store.
/// </remarks>
public class CustodiansTest : EnvironmentTestBase
{
    private readonly INativeSecretStore _native = Substitute.For<INativeSecretStore>();

    public CustodiansTest()
    {
        NativeStore.Current = _native;
        NativeCustodian.Logger = MockLogger;
    }

    protected override void TearDown()
    {
        NativeCustodian.Logger = null;
        base.TearDown();
    }

    private static NativeStoreException Unavailable()
    {
        return new NativeStoreException("NATIVE_UNAVAILABLE", "unavailable");
    }

    /// <remarks>Refer to "only an unavailable OS write permits file fallback"</remarks>
    [Fact]
    public void TestOnlyAnUnavailableOsWritePermitsFileFallback()
    {
        _native.When(store => store.Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())).Do(_ => throw Unavailable());

        StoreResult result = new NativeCustodian().Store("public", "private", new StoreContext { KeysFilepath = "custom.keys" });

        result.Fallback.ShouldBe("file");
        result.NativePrivateKeyAdded.ShouldBeFalse();
        MockLogger.WarningMessages.ShouldContain("OS secret store unavailable; saving private key to custom.keys");
    }

    /// <remarks>Refer to "OS readback mismatch and read errors never permit fallback"</remarks>
    [Fact]
    public void TestOsReadbackMismatchAndReadErrorsNeverPermitFallback()
    {
        _native.Get("public").Returns("wrong");
        NativeStoreException mismatch = Should.Throw<NativeStoreException>(() =>
            new NativeCustodian().Store("public", "private", new StoreContext { KeysFilepath = ".env.keys" }));
        mismatch.Code.ShouldBe("NATIVE_VERIFY_FAILED");

        _native.Get("public").Returns(_ => throw new NativeStoreException("NATIVE_UNAVAILABLE", "read failed"));
        Should.Throw<NativeStoreException>(() =>
            new NativeCustodian().Store("public", "private", new StoreContext { KeysFilepath = ".env.keys" })).Message.ShouldBe("read failed");

        MockLogger.WarningMessages.ShouldBeEmpty();
    }

    /// <remarks>Refer to "fallback stages file keys and preserves earlier staged keys"</remarks>
    [Fact]
    public void TestFallbackStagesFileKeysAndPreservesEarlierStagedKeys()
    {
        _native.When(store => store.Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())).Do(_ => throw Unavailable());

        StoreResult result = CustodiansRegistry.Store("native", "public", "private", new StoreContext
        {
            KeysSrc = "DOTENV_PRIVATE_KEY=existing\n", PrivateKeyName = "DOTENV_PRIVATE_KEY_PRODUCTION",
            Comment = ".env.production"
        });

        result.HasKeysSrc.ShouldBeTrue();
        result.KeysSrc.ShouldContain("DOTENV_PRIVATE_KEY=existing");
        result.KeysSrc.ShouldContain("DOTENV_PRIVATE_KEY_PRODUCTION=private");
    }

    /// <remarks>Refer to "write failures do not fall back to file custody"</remarks>
    [Fact]
    public void TestWriteFailuresDoNotFallBackToFileCustody()
    {
        _native.When(store => store.Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new NativeStoreException("NATIVE_ACCESS_FAILED", "verification failed"));

        Should.Throw<NativeStoreException>(() => CustodiansRegistry.Store("native", "public", "private", new StoreContext()))
            .Message.ShouldBe("verification failed");
        Should.Throw<Exception>(() => CustodiansRegistry.Store("unknown", "public", "private", new StoreContext()))
            .Message.ShouldContain("unknown custodian");
    }

    // Intentionally NOT ported upstream tests (approved by the user, 2026-10-15); kept as skipped tests so the upstream-test-titles
    // scan sees them and the reason is visible here. Add a new one only after asking.
    /// <remarks>Refer to "custom custodians participate in selection, writes and ordered lookup"</remarks>
    [Fact(Skip = "Not ported: Upstream builds registries from plugin objects (createRegistry, custom custodians, ordered lookup, disabled/unconfigured probing, synchronous-read validation). This port's registry is the fixed built-in set (file, native, armor, 1password, bitwarden) with no plugin API; the fallback and failure rules are covered by CustodiansTest.")]
    public void CustomCustodiansParticipateInSelectionWritesAndOrderedLookup()
    {
    }

    /// <remarks>Refer to "disabled custodians are never probed or read; unconfigured ones are not read"</remarks>
    [Fact(Skip = "Not ported: Upstream builds registries from plugin objects (createRegistry, custom custodians, ordered lookup, disabled/unconfigured probing, synchronous-read validation). This port's registry is the fixed built-in set (file, native, armor, 1password, bitwarden) with no plugin API; the fallback and failure rules are covered by CustodiansTest.")]
    public void DisabledCustodiansAreNeverProbedOrReadUnconfiguredOnesAreNotRead()
    {
    }

    /// <remarks>Refer to "invalid registration and unsupported synchronous reads fail explicitly"</remarks>
    [Fact(Skip = "Not ported: Upstream builds registries from plugin objects (createRegistry, custom custodians, ordered lookup, disabled/unconfigured probing, synchronous-read validation). This port's registry is the fixed built-in set (file, native, armor, 1password, bitwarden) with no plugin API; the fallback and failure rules are covered by CustodiansTest.")]
    public void InvalidRegistrationAndUnsupportedSynchronousReadsFailExplicitly()
    {
    }

}
