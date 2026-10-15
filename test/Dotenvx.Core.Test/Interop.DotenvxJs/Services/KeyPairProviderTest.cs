using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/tests/lib/resolvers/keypair.test.js (the provider-related scenarios). Upstream stubs the
/// primitives keyring; here the real keyring runs against a substitute native store (and a stubbed Armor keyring) so the
/// observable result - the private key found or not - shows which provider was passed.
/// </remarks>
public class KeyPairProviderTest : EnvironmentTestBase
{
    private const string PublicKey = "public-key";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-keypair-provider-" + Guid.NewGuid().ToString("N"));

    public KeyPairProviderTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
        Environment.SetEnvironmentVariable("CI", null);
        File.WriteAllText(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");
    }

    protected override void TearDown()
    {
        base.TearDown();
        Environment.CurrentDirectory = Path.GetTempPath();
        Directory.Delete(_dir, true);
    }

    /// <remarks>Refer to "keypair passes no provider when noArmor is true"</remarks>
    [Fact]
    public void TestKeypairPassesNoProviderWhenNoArmorIsTrue()
    {
        INativeSecretStore native = Substitute.For<INativeSecretStore>();
        native.Get(PublicKey).Returns("private-key");
        NativeStore.Current = native;
        NativeStore.Platform = "linux";

        var keyPair = new KeyPair(".env")
        {
            KeyAccess = new KeyAccessOptions { NoArmor = true, NoNative = true }
        };

        Dictionary<string, string?> result = keyPair.Run();

        result.ShouldBe(new Dictionary<string, string?> { ["DOTENV_PUBLIC_KEY"] = PublicKey, ["DOTENV_PRIVATE_KEY"] = null });
        native.DidNotReceive().Get(Arg.Any<string>());
    }

    /// <remarks>Refer to "keypair passes provider by default"</remarks>
    [Fact]
    public void TestKeypairPassesProviderByDefault()
    {
        INativeSecretStore native = Substitute.For<INativeSecretStore>();
        native.Get(PublicKey).Returns("private-key");
        NativeStore.Current = native;
        NativeStore.Platform = "linux";

        Dictionary<string, string?> result = new KeyPair(".env").Run();

        result.ShouldBe(new Dictionary<string, string?>
        {
            ["DOTENV_PUBLIC_KEY"] = PublicKey, ["DOTENV_PRIVATE_KEY"] = "private-key"
        });
    }


    private sealed class NotATty : IKeyInput
    {
        public bool IsTty => false;
        public bool IsRaw => false;
        public event Action<string>? Data { add { } remove { } }
        public void SetRawMode(bool raw) { }
        public void Resume() { }
        public void Pause() { }
    }

    // Intentionally NOT ported upstream tests (approved by the user, 2026-10-15); kept as skipped tests so the upstream-test-titles
    // scan sees them and the reason is visible here. Add a new one only after asking.
    /// <remarks>Refer to "keypair ${mode} reads envKeysFilepath before other providers"</remarks>
    [Fact(Skip = "Not ported: These stub JS module internals (proxyquire of the decryptor factory, the worker-thread sync variant, the resolver's provider chain) that have no counterpart here: key lookup runs through KeyProviders and the Dotenvx.Providers assembly, and Armor decryption through ParseWithDecryptor. The same behaviors (file keys before providers, --no-armor, provider origin reporting, Armor keyring requests with the Bearer token, unchanged output when Armor is off) are proven against the real Node CLI by ArmorProviderParityTest, NativeStoreParityTest and the keypair/get/decrypt parity tests.")]
    public void KeypairReadsEnvKeysFilepathBeforeOtherProviders()
    {
    }

    /// <remarks>Refer to "keypair ${mode} handles missing keys when no provider is enabled"</remarks>
    [Fact(Skip = "Not ported: These stub JS module internals (proxyquire of the decryptor factory, the worker-thread sync variant, the resolver's provider chain) that have no counterpart here: key lookup runs through KeyProviders and the Dotenvx.Providers assembly, and Armor decryption through ParseWithDecryptor. The same behaviors (file keys before providers, --no-armor, provider origin reporting, Armor keyring requests with the Bearer token, unchanged output when Armor is off) are proven against the real Node CLI by ArmorProviderParityTest, NativeStoreParityTest and the keypair/get/decrypt parity tests.")]
    public void KeypairHandlesMissingKeysWhenNoProviderIsEnabled()
    {
    }

    /// <remarks>Refer to "keypair ${mode} still reads file keys with noArmor"</remarks>
    [Fact(Skip = "Not ported: These stub JS module internals (proxyquire of the decryptor factory, the worker-thread sync variant, the resolver's provider chain) that have no counterpart here: key lookup runs through KeyProviders and the Dotenvx.Providers assembly, and Armor decryption through ParseWithDecryptor. The same behaviors (file keys before providers, --no-armor, provider origin reporting, Armor keyring requests with the Bearer token, unchanged output when Armor is off) are proven against the real Node CLI by ArmorProviderParityTest, NativeStoreParityTest and the keypair/get/decrypt parity tests.")]
    public void KeypairStillReadsFileKeysWithNoArmor()
    {
    }

    /// <remarks>Refer to "keypair ${mode} falls back to a provider when the file has no matching key"</remarks>
    [Fact(Skip = "Not ported: These stub JS module internals (proxyquire of the decryptor factory, the worker-thread sync variant, the resolver's provider chain) that have no counterpart here: key lookup runs through KeyProviders and the Dotenvx.Providers assembly, and Armor decryption through ParseWithDecryptor. The same behaviors (file keys before providers, --no-armor, provider origin reporting, Armor keyring requests with the Bearer token, unchanged output when Armor is off) are proven against the real Node CLI by ArmorProviderParityTest, NativeStoreParityTest and the keypair/get/decrypt parity tests.")]
    public void KeypairFallsBackToAProviderWhenTheFileHasNoMatchingKey()
    {
    }

    /// <remarks>Refer to "keypair ${mode} forwards onStatus and reports the provider origin"</remarks>
    [Fact(Skip = "Not ported: These stub JS module internals (proxyquire of the decryptor factory, the worker-thread sync variant, the resolver's provider chain) that have no counterpart here: key lookup runs through KeyProviders and the Dotenvx.Providers assembly, and Armor decryption through ParseWithDecryptor. The same behaviors (file keys before providers, --no-armor, provider origin reporting, Armor keyring requests with the Bearer token, unchanged output when Armor is off) are proven against the real Node CLI by ArmorProviderParityTest, NativeStoreParityTest and the keypair/get/decrypt parity tests.")]
    public void KeypairForwardsOnStatusAndReportsTheProviderOrigin()
    {
    }

}
