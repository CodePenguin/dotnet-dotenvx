using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/providers/armor/index.js, decryptors/armor/index.js and decryptors/index.js. Upstream
/// swaps Session/ArmorKeyring/PostArmorDecrypt with proxyquire classes; here the Session and keyring factories are the
/// seams and the HTTP layer is stubbed. The worker thread behind `decryptors.sync` has no counterpart (everything is
/// synchronous), so its test only asserts the same result.
/// </remarks>
public class ArmorProvidersTest : EnvironmentTestBase
{
    private const string PublicKeyHex = "027c9c5579cce25013e1e5ae8b4bde6d93bad14457babf5b3e055572ae4931f71";

    private sealed class SessionStub : Session
    {
        public override string Hostname() => "https://armor.example.com";
        public override string? Token() => "token-1";
        public override string DevicePublicKey() => "device-public-key";
        public override bool NoArmor() => NoArmorValue;
        public bool NoArmorValue { get; init; }
    }

    private sealed class KeyringStub(Func<ArmorKeyringStub, JsonNode> run)
        : ArmorKeyring(null, null, "", null)
    {
        public string? Hostname { get; set; }
        public string? Token { get; set; }
        public string? DevicePublicKey { get; set; }
        public string? PublicKeyHex { get; set; }

        public override JsonNode Run() => run(null!);
    }

    private sealed class ArmorKeyringStub;

    private KeyringStub Install(Func<KeyringStub, JsonNode> run, out List<KeyringStub> instances)
    {
        var created = new List<KeyringStub>();
        KeyringStub last = null!;
        ArmorProviders.SessionFactory = _ => new SessionStub();
        ArmorProviders.KeyringFactory = (hostname, token, device, publicKey) =>
        {
            KeyringStub stub = null!;
            stub = new KeyringStub(_ => run(stub))
            {
                Hostname = hostname, Token = token, DevicePublicKey = device, PublicKeyHex = publicKey
            };
            created.Add(stub);
            last = stub;

            return stub;
        };
        instances = created;

        return last;
    }

    /// <remarks>Refer to "armor provider forwards approval instructions to onStatus and opens url on Enter"</remarks>
    [Fact]
    public void TestArmorProviderForwardsApprovalInstructionsToOnStatusAndOpensUrlOnEnter()
    {
        var statuses = new List<string>();
        string? opened = null;
        OpenUrl.OpenAction = url => opened = url;
        var cleanups = 0;
        var stdin = new FakeTty(() => cleanups++);
        ListenForOpenKey.Input = () => stdin;
        Install(keyring =>
        {
            keyring.OnApprovalRequired!("https://armor.dotenvx.com/grants/grant-token-123", "ACCESS_APPROVAL_REQUIRED");

            return JsonNode.Parse("""{"public-key":"private-key"}""")!;
        }, out List<KeyringStub> instances);

        IDictionary<string, string> ring = ArmorProviders.Provider(PublicKeyHex, null, statuses.Add);

        ring.ShouldBe(new Dictionary<string, string> { ["public-key"] = "private-key" });
        instances.Count.ShouldBe(1);
        instances[0].Hostname.ShouldBe("https://armor.example.com");
        instances[0].Token.ShouldBe("token-1");
        instances[0].DevicePublicKey.ShouldBe("device-public-key");
        instances[0].PublicKeyHex.ShouldBe(PublicKeyHex);
        statuses.ShouldBe([
            "[ACCESS_APPROVAL_REQUIRED] press Enter to open [https://armor.dotenvx.com/grants/grant-token-123] and approve (027 C9C)"
        ]);
        stdin.Emit(((char)13).ToString());
        opened.ShouldBe("https://armor.dotenvx.com/grants/grant-token-123");
        cleanups.ShouldBe(1, "the open-key listener is cleaned up");
    }

    private sealed class FakeTty(Action onPause) : IKeyInput
    {
        public bool IsTty => true;
        public bool IsRaw => false;
        private Action<string>? _captured;

        // the provider cleans the listener up before returning: keep the handler so the test can still press Enter
        public event Action<string>? Data
        {
            add => _captured = value;
            remove { }
        }

        public void SetRawMode(bool raw) { }
        public void Resume() { }
        public void Pause() => onPause();
        public void Emit(string data) => _captured?.Invoke(data);
    }

    /// <remarks>Refer to "armor provider returns an empty keyring when armor has no matching keys"</remarks>
    [Fact]
    public void TestArmorProviderReturnsAnEmptyKeyringWhenArmorHasNoMatchingKeys()
    {
        Install(_ => JsonNode.Parse("{}")!, out _);

        ArmorProviders.Provider(PublicKeyHex).ShouldBeEmpty();
    }

    /// <remarks>Refer to "armor provider returns an empty keyring when Armor is offline"</remarks>
    [Fact]
    public void TestArmorProviderReturnsAnEmptyKeyringWhenArmorIsOffline()
    {
        Install(_ => throw new HttpRequestFailedException("ENOTFOUND", "getaddrinfo ENOTFOUND armor.example.com"),
            out _);

        ArmorProviders.Provider("public-key").ShouldBeEmpty();
    }

    /// <remarks>Refer to "armor provider preserves Armor API errors"</remarks>
    [Fact]
    public void TestArmorProviderPreservesArmorApiErrors()
    {
        var expected = new ApiException("SERVER_SIDE_DECRYPTION_REQUIRED",
            "[SERVER_SIDE_DECRYPTION_REQUIRED] server-side decryption required");
        Install(_ => throw expected, out _);

        Should.Throw<ApiException>(() => ArmorProviders.Provider("public-key")).ShouldBeSameAs(expected);
    }

    /// <remarks>Refer to "armor decryptor sends src and decryption context to PostArmorDecrypt"</remarks>
    [Fact]
    public void TestArmorDecryptorSendsSrcAndDecryptionContextToPostArmorDecrypt()
    {
        ArmorProviders.SessionFactory = _ => new SessionStub();
        string? url = null;
        string? body = null;
        IReadOnlyDictionary<string, string>? headers = null;
        ArmorHttp.Request = (u, _, h, b) =>
        {
            url = u;
            headers = h;
            body = b;

            return new HttpResult(200, """{"src":"HELLO=World\n"}""");
        };

        DecryptorResult result = ArmorProviders.DecryptSrc("HELLO=encrypted:ciphertext\n",
            new DecryptorOptions("public-key", "grant-token-1", null));

        url.ShouldBe("https://armor.example.com/api/armor/decrypt");
        headers!["Authorization"].ShouldBe("Bearer token-1");
        JsonNode sent = JsonNode.Parse(body!)!;
        sent["device_public_key"]!.ToString().ShouldBe("device-public-key");
        sent["public_key"]!.ToString().ShouldBe("public-key");
        sent["src"]!.ToString().ShouldBe("HELLO=encrypted:ciphertext\n");
        sent["grant_token"]!.ToString().ShouldBe("grant-token-1");
        result.Src.ShouldBe("HELLO=World\n");
    }

    /// <remarks>Refer to "decryptors returns a custom decryptor unchanged"</remarks>
    [Fact]
    public void TestDecryptorsReturnsACustomDecryptorUnchanged()
    {
        Decryptor custom = (_, _) => new DecryptorResult("");

        Providers.ResolveDecryptor(new KeyAccessOptions { HasCustomDecryptor = true, CustomDecryptor = custom })
            .ShouldBeSameAs(custom);
    }

    /// <remarks>Refer to "decryptors returns null when Armor is unavailable or disabled"</remarks>
    [Fact]
    public void TestDecryptorsReturnsNullWhenArmorIsUnavailableOrDisabled()
    {
        ArmorProviders.SessionFactory = _ => new SessionStub { NoArmorValue = true };
        Providers.ResolveDecryptor(new KeyAccessOptions()).ShouldBeNull();

        // `{ noArmor: true }` / `{ armor: false }`
        Providers.ResolveDecryptor(new KeyAccessOptions { NoArmor = true }).ShouldBeNull();
    }

    /// <remarks>Refer to "decryptors returns an Armor decryptor"</remarks>
    [Fact]
    public void TestDecryptorsReturnsAnArmorDecryptor()
    {
        DecryptorResult? seen = null;
        ArmorProviders.SessionFactory = _ => new SessionStub();
        ArmorHttp.Request = (_, _, _, _) => new HttpResult(200, """{"src":"HELLO=World\n"}""");
        Decryptor? decryptor = Providers.ResolveDecryptor(new KeyAccessOptions { Token = "token-1", Command = "run" });

        seen = decryptor!("HELLO=encrypted:ciphertext\n", new DecryptorOptions("public-key", "grant-token-1", null));

        seen.Src.ShouldBe("HELLO=World\n");
    }

    /// <remarks>Refer to "decryptors.sync runs the Armor decryptor through a worker"</remarks>
    [Fact]
    public void TestDecryptorsSyncRunsTheArmorDecryptorThroughAWorker()
    {
        // no worker thread in this port: the synchronous decryptor is the same code path
        ArmorProviders.SessionFactory = _ => new SessionStub();
        ArmorHttp.Request = (_, _, _, _) => new HttpResult(200, """{"src":"HELLO=World\n"}""");

        Decryptor? decryptor = Providers.ResolveDecryptor(new KeyAccessOptions { Token = "token-1", Command = "run" });

        decryptor!("HELLO=encrypted:ciphertext\n", new DecryptorOptions("public-key", "grant-token-1", null)).Src
            .ShouldBe("HELLO=World\n");
    }
}
