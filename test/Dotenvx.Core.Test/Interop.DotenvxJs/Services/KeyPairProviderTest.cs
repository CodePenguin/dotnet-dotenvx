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

    // Reference source for the five tests below: dotenvx/tests/lib/resolvers/keypair.test.js (`keypair ${mode} ...`). Upstream stubs the
    // file provider, the provider chain and the primitives keyring; here the real KeyPair runs against real key files and a
    // substitute native store. The async and sync variants are one implementation, so each title has one test.
    private const string RealPrivateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c";
    private const string OtherPrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    private static string RealPublicKey => global::Dotenvx.Primitives.Primitives.Derive(RealPrivateKey);

    private INativeSecretStore NativeHolding(string publicKey, string? privateKey)
    {
        INativeSecretStore native = Substitute.For<INativeSecretStore>();
        native.Get(publicKey).Returns(privateKey);
        NativeStore.Current = native;
        NativeStore.Platform = "linux";

        return native;
    }

    private void WriteEnv()
    {
        File.WriteAllText(".env", $"DOTENV_PUBLIC_KEY=\"{RealPublicKey}\"\n");
    }

    /// <remarks>Refer to "keypair ${mode} reads envKeysFilepath before other providers"</remarks>
    [Fact]
    public void KeypairReadsEnvKeysFilepathBeforeOtherProviders()
    {
        WriteEnv();
        File.WriteAllText(".env.custom.keys", $"DOTENV_PRIVATE_KEY={RealPrivateKey}\n");
        INativeSecretStore native = NativeHolding(RealPublicKey, OtherPrivateKey);
        var keyPair = new KeyPair(".env", [".env.custom.keys"]);

        Dictionary<string, string?> result = keyPair.Run();

        result["DOTENV_PRIVATE_KEY"].ShouldBe(RealPrivateKey);
        keyPair.Origin.ShouldBe(".env.keys");
        native.DidNotReceive().Get(Arg.Any<string>());
    }

    /// <remarks>Refer to "keypair ${mode} handles missing keys when no provider is enabled"</remarks>
    [Fact]
    public void KeypairHandlesMissingKeysWhenNoProviderIsEnabled()
    {
        WriteEnv();
        var keyPair = new KeyPair(".env") { KeyAccess = new KeyAccessOptions { NoArmor = true, NoNative = true } };

        Dictionary<string, string?> result = keyPair.Run();

        result.ShouldBe(new Dictionary<string, string?> { ["DOTENV_PUBLIC_KEY"] = RealPublicKey, ["DOTENV_PRIVATE_KEY"] = null });
        keyPair.Origin.ShouldBeNull();
    }

    /// <remarks>Refer to "keypair ${mode} still reads file keys with noArmor"</remarks>
    [Fact]
    public void KeypairStillReadsFileKeysWithNoArmor()
    {
        WriteEnv();
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={RealPrivateKey}\n");
        var keyPair = new KeyPair(".env") { KeyAccess = new KeyAccessOptions { NoArmor = true, NoNative = true } };

        keyPair.Run().ShouldBe(new Dictionary<string, string?>
        {
            ["DOTENV_PUBLIC_KEY"] = RealPublicKey, ["DOTENV_PRIVATE_KEY"] = RealPrivateKey
        });
        keyPair.Origin.ShouldBe(".env.keys");
    }

    /// <remarks>Refer to "keypair ${mode} falls back to a provider when the file has no matching key"</remarks>
    [Fact]
    public void KeypairFallsBackToAProviderWhenTheFileHasNoMatchingKey()
    {
        WriteEnv();
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={OtherPrivateKey}\n");
        INativeSecretStore native = NativeHolding(RealPublicKey, RealPrivateKey);
        var keyPair = new KeyPair(".env") { KeyAccess = new KeyAccessOptions { NoArmor = true } };

        Dictionary<string, string?> result = keyPair.Run();

        result["DOTENV_PRIVATE_KEY"].ShouldBe(RealPrivateKey);
        keyPair.Origin.ShouldBe("linuxSecretService");
        native.Received(1).Get(RealPublicKey);
    }

    /// <remarks>Refer to "keypair ${mode} forwards onStatus and reports the provider origin"</remarks>
    [Fact]
    public void KeypairForwardsOnStatusAndReportsTheProviderOrigin()
    {
        // Armor is stubbed at the HTTP seam (ArmorHttp.Request): the first keyring call asks for access approval, the call that
        // carries the grant token returns the key. The resolver supplies its own `onProvider`
        // (keypair.js: `{ ...options, onProvider: name => { origin = name } }`), so the origin is reported through `Origin` and the
        // caller's callback is left alone.
        WriteEnv();
        var requests = new List<string>();
        ArmorHttp.Request = (url, _, _, body) =>
        {
            requests.Add(body ?? "");
            if (body is null || !body.Contains("grant_token"))
            {
                return new HttpResult(403,
                    """{"error":{"code":"ACCESS_APPROVAL_REQUIRED","message":"approval needed","meta":{"grant_token":"grant1","approval_uri":"https://armor.example/approve"}}}""");
            }

            return new HttpResult(200, $$"""{"{{RealPublicKey}}":"{{RealPrivateKey}}"}""");
        };
        var statuses = new List<string>();
        var reported = new List<string>();
        Action<string, string> callersCallback = (name, _) => reported.Add(name);
        var keyPair = new KeyPair(".env")
        {
            KeyAccess = new KeyAccessOptions
            {
                NoNative = true, Token = "token-1", OnStatus = statuses.Add, OnProvider = callersCallback, Interactive = true
            }
        };

        Dictionary<string, string?> result = keyPair.Run();

        result["DOTENV_PRIVATE_KEY"].ShouldBe(RealPrivateKey);
        keyPair.Origin.ShouldBe("armor");
        statuses.ShouldBe([$"[ACCESS_APPROVAL_REQUIRED] press Enter to open [https://armor.example/approve] and approve ({ArmoredKeyDisplay.Display(RealPublicKey)})"]);
        requests.Count.ShouldBe(2);
        reported.ShouldBeEmpty();
        keyPair.KeyAccess.OnProvider.ShouldBeSameAs(callersCallback);
    }
}
