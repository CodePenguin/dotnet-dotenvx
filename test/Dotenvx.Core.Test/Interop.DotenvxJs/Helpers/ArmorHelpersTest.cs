using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{armoredKeyDisplay,buildApiError,buildOauthError,encryptDeviceValue,
/// decryptDeviceValue,isNetworkError,jsonToEnv,formatCode,http,openUrl,listenForOpenKey,teamChoicesFromMeta}.js. Upstream
/// replaces `process.stdin` with an EventEmitter for listenForOpenKey; here the keyboard is the swappable
/// <see cref="IKeyInput" />.
/// </remarks>
public class ArmorHelpersTest : EnvironmentTestBase
{
    /// <remarks>Refer to "armoredKeyDisplay formats first six public key characters"</remarks>
    [Fact]
    public void TestArmoredKeyDisplayFormatsFirstSixPublicKeyCharacters()
    {
        ArmoredKeyDisplay.Display("027c9c5579cce25013e1e5ae8b4bde6d93bad14457babf5b3e055572ae4931f71").ShouldBe("027 C9C");
        ArmoredKeyDisplay.Display("abcdef").ShouldBe("ABC DEF");
        ArmoredKeyDisplay.Display("abc").ShouldBe("ABC");
        ArmoredKeyDisplay.Display("").ShouldBe("");
    }

    /// <remarks>Refer to "buildApiError includes API help in messageWithHelp"</remarks>
    [Fact]
    public void TestBuildApiErrorIncludesApiHelpInMessageWithHelp()
    {
        ApiException error = ArmorApi.BuildApiError(400, JsonNode.Parse("""
            {"error":{"code":"NO_TEAMS","message":"create or join a team before running this command","help":"https://github.com/dotenvx/dotenvx/issues/924","meta":null}}
            """)!);

        error.Message.ShouldBe("[NO_TEAMS] create or join a team before running this command");
        error.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/924]");
        error.MessageWithHelp.ShouldBe(
            "[NO_TEAMS] create or join a team before running this command. fix: [https://github.com/dotenvx/dotenvx/issues/924]");
    }

    /// <remarks>Refer to "buildApiError omits messageWithHelp when API help is absent"</remarks>
    [Fact]
    public void TestBuildApiErrorOmitsMessageWithHelpWhenApiHelpIsAbsent()
    {
        ApiException error = ArmorApi.BuildApiError(400, JsonNode.Parse("""
            {"error":{"code":"BAD_REQUEST","message":"bad request","help":null,"meta":null}}
            """)!);

        error.Message.ShouldBe("[BAD_REQUEST] bad request");
        // upstream: `ct.notOk(error.messageWithHelp)`; the plain message is what this port's MessageWithHelp falls back to
        error.MessageWithHelp.ShouldBe("[BAD_REQUEST] bad request");
        error.Help.ShouldBeNull();
    }

    /// <remarks>Refer to "device value encryption stores prefixless ciphertext"</remarks>
    [Fact]
    public void TestDeviceValueEncryptionStoresPrefixlessCiphertext()
    {
        (string publicKey, string privateKey) = Primitives.Primitives.Keypair();

        string encrypted = Primitives.Primitives.Encrypt("hello", publicKey, false);

        encrypted.ShouldNotStartWith("encrypted:");
        Primitives.Primitives.Decrypt(privateKey, encrypted, false).ShouldBe("hello");
    }

    /// <remarks>Refer to "device value encryption round trips multiline values"</remarks>
    [Fact]
    public void TestDeviceValueEncryptionRoundTripsMultilineValues()
    {
        (string publicKey, string privateKey) = Primitives.Primitives.Keypair();
        string value = "line 1\nline 2\nline 3";

        string encrypted = Primitives.Primitives.Encrypt(value, publicKey, false);

        Primitives.Primitives.Decrypt(privateKey, encrypted, false).ShouldBe(value);
    }

    /// <remarks>Refer to "isNetworkError recognizes direct and wrapped connectivity errors"</remarks>
    [Fact]
    public void TestIsNetworkErrorRecognizesDirectAndWrappedConnectivityErrors()
    {
        ArmorHttp.IsNetworkError(new HttpRequestFailedException("ENETUNREACH", "x")).ShouldBeTrue();
        ArmorHttp.IsNetworkError(new HttpRequestFailedException("UND_ERR_CONNECT_TIMEOUT", "x")).ShouldBeTrue();
        // `{ cause: { code: 'ENOTFOUND' } }`: a wrapped error counts too
        ArmorHttp.IsNetworkError(new Exception("fetch failed", new HttpRequestFailedException("ENOTFOUND", "x")))
            .ShouldBeTrue();
        ArmorHttp.IsNetworkError(new ApiException("SERVER_SIDE_DECRYPTION_REQUIRED", "x")).ShouldBeFalse();
        ArmorHttp.IsNetworkError(new Exception("unknown")).ShouldBeFalse();
    }

    /// <remarks>Refer to "jsonToEnv serializes settings as dotenv assignments"</remarks>
    [Fact]
    public void TestJsonToEnvSerializesSettingsAsDotenvAssignments()
    {
        Session.JsonToEnv(new Dictionary<string, object> { ["A"] = "one", ["B"] = "two" }).ShouldBe("A=\"one\"\nB=\"two\"");
    }

    /// <remarks>Refer to "buildOauthError attaches oauth code, help, and statusCode"</remarks>
    [Fact]
    public void TestBuildOauthErrorAttachesOauthCodeHelpAndStatusCode()
    {
        ApiException error = ArmorApi.BuildOauthError(400, JsonNode.Parse("""
            {"error":"authorization_pending","error_description":"still waiting"}
            """)!);

        error.Message.ShouldBe("[authorization_pending] still waiting");
        error.Code.ShouldBe("authorization_pending");
        error.StatusCode.ShouldBe(400);
        // upstream also sets `help` to `[code] <json>` (never printed); this port keeps the raw JSON on the error instead
        error.Json!.ToJsonString().ShouldBe("""{"error":"authorization_pending","error_description":"still waiting"}""");
    }

    /// <remarks>Refer to "formatCode groups user code in fours"</remarks>
    [Fact]
    public void TestFormatCodeGroupsUserCodeInFours()
    {
        FormatCodeHelper.FormatCode("ABCD1234EF").ShouldBe("ABCD-1234-EF");
    }

    /// <remarks>Refer to "http delegates to undici request"</remarks>
    [Fact]
    public void TestHttpDelegatesToTheRequestFunction()
    {
        string? seenUrl = null;
        string? seenMethod = null;
        ArmorHttp.Request = (url, method, _, _) =>
        {
            seenUrl = url;
            seenMethod = method;

            return new HttpResult(200, "ok");
        };

        HttpResult result = ArmorHttp.Request("https://armor.dotenvx.com/oauth/token", "POST", null, null);

        result.Body.ShouldBe("ok");
        seenUrl.ShouldBe("https://armor.dotenvx.com/oauth/token");
        seenMethod.ShouldBe("POST");
    }

    /// <remarks>Refer to "openUrl opens without waiting"</remarks>
    [Fact]
    public void TestOpenUrlOpensWithoutWaiting()
    {
        string? opened = null;
        OpenUrl.OpenAction = url => opened = url;

        OpenUrl.Open("https://armor.dotenvx.com/device");

        opened.ShouldBe("https://armor.dotenvx.com/device");
    }

    private sealed class FakeKeyInput(bool isTty, bool isRaw) : IKeyInput
    {
        public bool IsTty { get; } = isTty;
        public bool IsRaw { get; } = isRaw;
        public List<bool> RawModes { get; } = [];
        public int Resumed { get; private set; }
        public int Paused { get; private set; }

        public event Action<string>? Data;

        public void SetRawMode(bool raw)
        {
            RawModes.Add(raw);
        }

        public void Resume()
        {
            Resumed++;
        }

        public void Pause()
        {
            Paused++;
        }

        public void Emit(string data)
        {
            Data?.Invoke(data);
        }
    }

    /// <remarks>Refer to "listenForOpenKey returns noop when stdin is not a tty"</remarks>
    [Fact]
    public void TestListenForOpenKeyReturnsNoopWhenStdinIsNotATty()
    {
        ListenForOpenKey.Input = () => new FakeKeyInput(false, false);

        Action cleanup = ListenForOpenKey.Listen(() => { });

        cleanup.ShouldNotBeNull();
        cleanup();
    }

    /// <remarks>Refer to "listenForOpenKey opens once and restores stdin on cleanup"</remarks>
    [Fact]
    public void TestListenForOpenKeyOpensOnceAndRestoresStdinOnCleanup()
    {
        var stdin = new FakeKeyInput(true, false);
        ListenForOpenKey.Input = () => stdin;
        int opened = 0;

        Action cleanup = ListenForOpenKey.Listen(() => opened++);
        stdin.Emit(((char)13).ToString());
        stdin.Emit("y");
        cleanup();

        opened.ShouldBe(1);
        stdin.RawModes.ShouldBe([true, false]);
        stdin.Resumed.ShouldBe(1);
        stdin.Paused.ShouldBe(1);
    }

    /// <remarks>Refer to "listenForOpenKey handles n and ctrl-c as interrupts"</remarks>
    [Fact]
    public void TestListenForOpenKeyHandlesNAndCtrlCAsInterrupts()
    {
        var stdin = new FakeKeyInput(true, true);
        ListenForOpenKey.Input = () => stdin;
        int interrupts = 0;
        ListenForOpenKey.Interrupt = () => interrupts++;

        ListenForOpenKey.Listen(() => { });
        stdin.Emit("n");
        ListenForOpenKey.Listen(() => { });
        stdin.Emit(((char)3).ToString());

        interrupts.ShouldBe(2);
    }

    /// <remarks>Refer to "maps organizations to prompt choices"</remarks>
    [Fact]
    public void TestMapsOrganizationsToPromptChoices()
    {
        Prompts.Choice[] choices = ArmorTeam.ChoicesFromMeta(JsonNode.Parse("""
            {"organizations":[{"provider_slug":"dotenvx"},{"provider_slug":"motdotla"}]}
            """));

        choices.ShouldBe([new Prompts.Choice("dotenvx", "dotenvx"), new Prompts.Choice("motdotla", "motdotla")]);
    }
}
