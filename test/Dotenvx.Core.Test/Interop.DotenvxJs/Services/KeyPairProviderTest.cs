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

    private sealed class ApprovalKeyring : ArmorKeyring
    {
        public ApprovalKeyring() : base(null, null, "", null)
        {
        }

        public override JsonNode Run()
        {
            OnApprovalRequired?.Invoke("https://armor.example/approve", "ACCESS_APPROVAL_REQUIRED");

            return JsonNode.Parse("""{"public-key":"private-key"}""")!;
        }
    }

    /// <remarks>Refer to "keypair forwards onStatus to providers"</remarks>
    [Fact]
    public void TestKeypairForwardsOnStatusToProviders()
    {
        var statuses = new List<string>();
        ArmorProviders.KeyringFactory = (_, _, _, _) => new ApprovalKeyring();
        ListenForOpenKey.Input = () => new NotATty();
        // an active Armor session (settings file present, logged in) so the Armor provider is composed
        string config = Environment.GetEnvironmentVariable("DOTENVX_CONFIG")!;
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ".env"),
            "DOTENVX_ARMOR_USERNAME=\"scott\"\nDOTENVX_ARMOR_TOKEN=\"token-123\"\nDOTENVX_ARMOR_ON=\"true\"\n");

        Dictionary<string, string?> result = new KeyPair(".env")
        {
            KeyAccess = new KeyAccessOptions { NoNative = true, OnStatus = statuses.Add }
        }.Run();

        result["DOTENV_PRIVATE_KEY"].ShouldBe("private-key");
        statuses.ShouldBe(["[ACCESS_APPROVAL_REQUIRED] press Enter to open [https://armor.example/approve] and approve (PUB LIC)"]);
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
}
