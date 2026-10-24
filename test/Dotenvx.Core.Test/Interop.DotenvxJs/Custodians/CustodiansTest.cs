using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Helpers;

using NSubstitute;

using Shouldly;

using CustodiansRegistry = global::Dotenvx.Interop.DotenvxJs.Custodians.Custodians;
using NativeStoreException = global::Dotenvx.Providers.NativeStoreException;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Custodians;

/// <remarks>
/// Reference source: dotenvx/tests/lib/custodians/{index,native}.test.js. Upstream builds registries from plugin objects
/// (`createRegistry`) and proxyquires the native backend; here `CustodianRegistry` takes ICustodian plugins, and the fallback and
/// failure rules run against a substitute OS store.
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
            KeysSrc = "DOTENV_PRIVATE_KEY=existing\n",
            PrivateKeyName = "DOTENV_PRIVATE_KEY_PRODUCTION",
            Comment = ".env.production"
        });

        result.HasKeysSrc.ShouldBeTrue();
        result.KeysSrc!.ShouldContain("DOTENV_PRIVATE_KEY=existing");
        result.KeysSrc!.ShouldContain("DOTENV_PRIVATE_KEY_PRODUCTION=private");
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

    // The three registry scenarios of dotenvx/tests/lib/custodians/index.test.js, which build registries from plugin objects
    // (`createRegistry`); a plugin here is an ICustodian (ISyncCustodian when it can also be read synchronously).
    private class Plugin(string id) : ICustodian
    {
        public string Id { get; } = id;
        public string Name { get; init; } = id;
        public string Custody { get; init; } = "local";
        public Func<bool> IsEnabled { get; init; } = () => true;
        public Func<bool> IsAvailable { get; init; } = () => true;
        public Func<bool> IsConfigured { get; init; } = () => true;
        public Func<string, IDictionary<string, string>>? Reader { get; init; }
        public Func<string, string, StoreContext, StoreResult?> Writer { get; init; } = (_, _, _) => null;
        public bool CanGet => Reader is not null;
        public bool Enabled(KeyAccessOptions? options = null) => IsEnabled();
        public bool Available() => IsAvailable();
        public bool Configured(KeyAccessOptions? options = null) => IsConfigured();
        public IDictionary<string, string> Get(string publicKey) => Reader!(publicKey);
        public StoreResult? Store(string publicKey, string privateKey, StoreContext context) => Writer(publicKey, privateKey, context);
    }

    private sealed class SyncPlugin(string id) : Plugin(id), ISyncCustodian
    {
        public Func<string, IDictionary<string, string>>? SyncReader { get; init; }
        public IDictionary<string, string> GetSync(string publicKey) => SyncReader!(publicKey);
    }

    private static Dictionary<string, string> Ring(string key, string value) => new() { [key] = value };

    /// <remarks>Refer to "custom custodians participate in selection, writes and ordered lookup"</remarks>
    [Fact]
    public void CustomCustodiansParticipateInSelectionWritesAndOrderedLookup()
    {
        var writes = new List<(string, string, StoreContext)>();
        var custom = new SyncPlugin("custom")
        {
            Reader = key => Ring(key, "custom"),
            SyncReader = key => Ring(key, "custom"),
            Writer = (publicKey, privateKey, context) =>
            {
                writes.Add((publicKey, privateKey, context));

                return null;
            }
        };
        CustodianRegistry registry = CustodiansRegistry.CreateRegistry([custom, new FileCustodian()]);

        registry.Choices().ShouldBe([new CustodianChoice("custom", "custom", false), new CustodianChoice("File (.env.keys)", "file", false)]);
        var context = new StoreContext { Comment = ".env" };
        registry.Store("custom", "public", "private", context).ShouldBeEquivalentTo(new StoreResult());
        writes.ShouldBe([("public", "private", context)]);
        registry.Providers()[0]("public").ShouldBe(Ring("public", "custom"));
        registry.Providers(sync: true)[0]("public").ShouldBe(Ring("public", "custom"));
        registry.Providers().Count.ShouldBe(1, "file keys remain in the existing env resolution layer");
    }

    /// <remarks>Refer to "disabled custodians are never probed or read; unconfigured ones are not read"</remarks>
    [Fact]
    public void DisabledCustodiansAreNeverProbedOrReadUnconfiguredOnesAreNotRead()
    {
        CustodianRegistry registry = CustodiansRegistry.CreateRegistry(
        [
            new Plugin("disabled")
            {
                IsEnabled = () => false, IsAvailable = () => throw new Exception("must not probe"), Reader = _ => Ring("x", "x")
            },
            new Plugin("unconfigured") { IsConfigured = () => false, Reader = _ => throw new Exception("must not read") },
            new SyncPlugin("first") { Reader = key => Ring(key, "first"), SyncReader = key => Ring(key, "first-sync") },
            new SyncPlugin("second") { Reader = key => Ring(key, "second"), SyncReader = key => Ring(key, "second-sync") }
        ]);

        registry.Choices()[0].Disabled.ShouldBeTrue();
        registry.Providers().Select(provider => provider("key")).ShouldBe([Ring("key", "first"), Ring("key", "second")]);
        registry.Providers(sync: true).Select(provider => provider("key")).ShouldBe([Ring("key", "first-sync"), Ring("key", "second-sync")]);
    }

    /// <remarks>Refer to "invalid registration and unsupported synchronous reads fail explicitly"</remarks>
    [Fact]
    public void InvalidRegistrationAndUnsupportedSynchronousReadsFailExplicitly()
    {
        // upstream's second assertion (`createRegistry([{ id: 'bad' }])` -> /requires enabled/) checks a duck-typed object; here the
        // interface guarantees enabled/available/store, so only the id rules and the synchronous read rule remain to be checked
        Should.Throw<Exception>(() => CustodiansRegistry.CreateRegistry([new Plugin("same"), new Plugin("same")])).Message.ShouldContain("unique");
        Should.Throw<Exception>(() => CustodiansRegistry.CreateRegistry([new Plugin("")])).Message.ShouldContain("unique");
        CustodianRegistry registry = CustodiansRegistry.CreateRegistry([new Plugin("async") { Reader = _ => new Dictionary<string, string>() }]);

        Should.Throw<Exception>(() => registry.Providers(sync: true)).Message.ShouldContain("does not support synchronous reads");
        registry.Providers().Count.ShouldBe(1);
    }
}
