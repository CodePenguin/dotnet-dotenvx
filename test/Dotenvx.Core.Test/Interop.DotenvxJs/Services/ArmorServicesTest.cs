using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/{armorUp,armorDown,armorPull,armorPush,armorMove,login,logout}.js. Upstream
/// replaces readEnvKey/removeEnvKey/prompts/Post* with stubs; here the real helpers work on temp files, the HTTP seam
/// answers the API calls and `Prompts.Select` is stubbed, so every assertion is about the requests sent and the files left
/// behind. (The JS services spread the API json into their result; the C# result carries the fields the actions use.)
/// </remarks>
public class ArmorServicesTest : EnvironmentTestBase
{
    private static readonly (string PublicKey, string PrivateKey) Pair = global::Dotenvx.Primitives.Primitives.Keypair();
    private static string Pub => Pair.PublicKey;
    private static string Priv => Pair.PrivateKey;

    private readonly List<(string Path, JsonNode Body)> _calls = [];
    private readonly List<string> _selectMessages = [];
    private readonly string _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dotenvx-armor-svc-" + Guid.NewGuid().ToString("N"));

    public ArmorServicesTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
        PackageJson.Version = "0.0.0-test";
    }

    protected override void TearDown()
    {
        base.TearDown();
        Environment.CurrentDirectory = System.IO.Path.GetTempPath();
        Directory.Delete(_dir, true);
    }

    private void Api(Func<string, JsonNode, (int Status, string Body)> handler)
    {
        ArmorHttp.Request = (url, _, _, body) =>
        {
            string path = new Uri(url).AbsolutePath;
            JsonNode json = body is null ? new JsonObject() : JsonNode.Parse(body)!;
            _calls.Add((path, json));
            (int status, string text) = handler(path, json);

            return new HttpResult(status, text);
        };
    }

    private void Select(string? answer)
    {
        Prompts.Select = request =>
        {
            _selectMessages.Add(request.Message);

            return answer;
        };
    }

    private static string TeamRequired(params string[] teams)
    {
        string orgs = string.Join(",", teams.Select(team => $"{{\"provider_slug\":\"{team}\"}}"));

        return """{"error":{"code":"TEAM_REQUIRED","message":"team required","meta":{"organizations":[ORGS]}}}""".Replace("ORGS", orgs);
    }

    private void Env(string file, string? publicKey = null)
    {
        File.WriteAllText(file, $"{GuessPublicKey(file)}=\"{publicKey ?? Pub}\"\n");
    }

    private static string GuessPublicKey(string file)
    {
        return file == ".env" ? "DOTENV_PUBLIC_KEY" : "DOTENV_PUBLIC_KEY_" + file[5..].ToUpperInvariant();
    }

    private static string KeyName(string file)
    {
        return file == ".env" ? "DOTENV_PRIVATE_KEY" : "DOTENV_PRIVATE_KEY_" + file[5..].ToUpperInvariant();
    }

    private static void Keys(string file, string? value = null)
    {
        File.WriteAllText(".env.keys", $"{KeyName(file)}=\"{value ?? Priv}\"\n");
    }

    // ---- armorUp ----

    /// <remarks>Refer to "ArmorUp pushes private key and removes it from .env.keys"</remarks>
    [Fact]
    public void TestArmorUpPushesPrivateKeyAndRemovesItFromEnvKeys()
    {
        Env(".env.production");
        Keys(".env.production");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":false}"""));

        ArmorResult result = new ArmorUp("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env.production").Run();

        _selectMessages.ShouldBeEmpty("does not prompt when armor up succeeds without team");
        _calls[0].Body["public_key"]!.ToString().ShouldBe(Pub);
        _calls[0].Body["private_key"]!.ToString().ShouldBe(Priv);
        _calls[0].Body["team"].ShouldBeNull();
        File.Exists(".env.keys").ShouldBeFalse("removes the local private key after a successful armor up");  // a pushed local key counts as changed (armorUp.js: changed = json.changed || privateKey != null)
        result.ShouldBe(new ArmorResult(true, "DOTENV_PRIVATE_KEY_PRODUCTION", "priv-from-api", Pub));
    }

    /// <remarks>Refer to "ArmorUp still calls api when local private key is already absent"</remarks>
    [Fact]
    public void TestArmorUpStillCallsApiWhenLocalPrivateKeyIsAlreadyAbsent()
    {
        Env(".env");
        File.WriteAllText(".env.keys", "OTHER=1\n");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true}"""));

        ArmorResult result = new ArmorUp("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _calls[0].Body.AsObject().ContainsKey("private_key").ShouldBeFalse("sends public key without private key or team");
        File.ReadAllText(".env.keys").ShouldBe("OTHER=1\n");
        result.Changed.ShouldBeTrue("surfaces remote changed flag");
    }

    /// <remarks>Refer to "ArmorUp sends explicit team without prompt or retry"</remarks>
    [Fact]
    public void TestArmorUpSendsExplicitTeamWithoutPromptOrRetry()
    {
        Env(".env.production");
        Keys(".env.production");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true,"team":"hackclub"}"""));

        ArmorResult result = new ArmorUp("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env.production", "hackclub").Run();

        _selectMessages.ShouldBeEmpty();
        _calls.Count.ShouldBe(1);
        _calls[0].Body["team"]!.ToString().ShouldBe("hackclub");
        File.Exists(".env.keys").ShouldBeFalse();
        result.ShouldBe(new ArmorResult(true, "DOTENV_PRIVATE_KEY_PRODUCTION", "priv-from-api", Pub));
    }

    /// <remarks>Refer to "ArmorUp retries with selected team when api requires team"</remarks>
    [Fact]
    public void TestArmorUpRetriesWithSelectedTeamWhenApiRequiresTeam()
    {
        Env(".env.production");
        Keys(".env.production");
        Select("globex");
        Api((_, body) => body["team"] is null
            ? (400, TeamRequired("acme", "globex"))
            : (200, """{"private_key":"priv-from-api","changed":true}"""));

        ArmorResult result = new ArmorUp("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env.production").Run();

        _selectMessages.ShouldBe(["Select team"]);
        _calls.Count.ShouldBe(2);
        _calls[1].Body["team"]!.ToString().ShouldBe("globex");
        result.Changed.ShouldBeTrue();
    }

    /// <remarks>Refer to "ArmorUp rethrows non-team-required api errors"</remarks>
    [Fact]
    public void TestArmorUpRethrowsNonTeamRequiredApiErrors()
    {
        Env(".env");
        Keys(".env");
        Api((_, _) => (401, """{"error":{"code":"UNAUTHORIZED","message":"bad token"}}"""));

        Should.Throw<ApiException>(() => new ArmorUp("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run())
            .Code.ShouldBe("UNAUTHORIZED");

        _selectMessages.ShouldBeEmpty();
        File.Exists(".env.keys").ShouldBeTrue("a failed armor up leaves the local key alone");
    }

    // ---- armorDown ----

    /// <remarks>Refer to "ArmorDown pulls private key back into .env.keys"</remarks>
    [Fact]
    public void TestArmorDownPullsPrivateKeyBackIntoEnvKeys()
    {
        Env(".env.production");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true}"""));

        ArmorResult result = new ArmorDown("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env.production").Run();

        _selectMessages.ShouldBeEmpty();
        _calls[0].Body["public_key"]!.ToString().ShouldBe(Pub);
        File.ReadAllText(".env.keys").ShouldBe("DOTENV_PRIVATE_KEY_PRODUCTION=priv-from-api\n");
        result.ShouldBe(new ArmorResult(true, "DOTENV_PRIVATE_KEY_PRODUCTION", "priv-from-api", Pub));
    }

    /// <remarks>Refer to "ArmorDown surfaces remote changed flag even if local file was already current"</remarks>
    [Fact]
    public void TestArmorDownSurfacesRemoteChangedFlagEvenIfLocalFileWasAlreadyCurrent()
    {
        Env(".env");
        File.WriteAllText(".env.keys", "DOTENV_PRIVATE_KEY=priv-from-api\n");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorDown("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run().Changed.ShouldBeTrue();
    }

    /// <remarks>Refer to "ArmorDown sends explicit team without prompt or retry"</remarks>
    [Fact]
    public void TestArmorDownSendsExplicitTeamWithoutPromptOrRetry()
    {
        Env(".env");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorDown("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env", "hackclub").Run();

        _selectMessages.ShouldBeEmpty();
        _calls.Count.ShouldBe(1);
        _calls[0].Body["team"]!.ToString().ShouldBe("hackclub");
    }

    /// <remarks>Refer to "ArmorDown prompts for team and retries when api requires team"</remarks>
    [Fact]
    public void TestArmorDownPromptsForTeamAndRetriesWhenApiRequiresTeam()
    {
        Env(".env");
        Select("globex");
        Api((_, body) => body["team"] is null
            ? (400, TeamRequired("acme", "globex"))
            : (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorDown("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBe(["Select team"]);
        _calls[1].Body["team"]!.ToString().ShouldBe("globex");
    }

    /// <remarks>Refer to "ArmorDown uses only team from required error meta without prompting"</remarks>
    [Fact]
    public void TestArmorDownUsesOnlyTeamFromRequiredErrorMetaWithoutPrompting()
    {
        Env(".env");
        Api((_, body) => body["team"] is null
            ? (400, TeamRequired("solo"))
            : (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorDown("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBeEmpty();
        _calls[1].Body["team"]!.ToString().ShouldBe("solo");
    }

    /// <remarks>Refer to "ArmorDown rethrows non-team-required api errors"</remarks>
    [Fact]
    public void TestArmorDownRethrowsNonTeamRequiredApiErrors()
    {
        Env(".env");
        Api((_, _) => (500, """{"error":{"code":"BOOM","message":"boom"}}"""));

        Should.Throw<ApiException>(() => new ArmorDown("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run())
            .Code.ShouldBe("BOOM");
    }

    // ---- armorPull ----

    /// <remarks>Refer to "ArmorPull reads environment-specific public key and upserts private key"</remarks>
    [Fact]
    public void TestArmorPullReadsEnvironmentSpecificPublicKeyAndUpsertsPrivateKey()
    {
        Env(".env.production");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":false}"""));

        ArmorResult result = new ArmorPull("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env.production").Run();

        _calls[0].Body["public_key"]!.ToString().ShouldBe(Pub);
        File.ReadAllText(".env.keys").ShouldBe("DOTENV_PRIVATE_KEY_PRODUCTION=priv-from-api\n");
        // armorPull.js: `changed` is the upsert's (the file changed), not the API's flag
        result.ShouldBe(new ArmorResult(true, "DOTENV_PRIVATE_KEY_PRODUCTION", "priv-from-api", Pub));
    }

    /// <remarks>Refer to "ArmorPull returns unchanged when .env.keys already has the private key"</remarks>
    [Fact]
    public void TestArmorPullReturnsUnchangedWhenEnvKeysAlreadyHasThePrivateKey()
    {
        Env(".env");
        File.WriteAllText(".env.keys", "DOTENV_PRIVATE_KEY=priv-from-api\n");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorPull("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run().Changed.ShouldBeFalse();
    }

    /// <remarks>Refer to "ArmorPull sends explicit team without prompt or retry"</remarks>
    [Fact]
    public void TestArmorPullSendsExplicitTeamWithoutPromptOrRetry()
    {
        Env(".env");
        Api((_, _) => (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorPull("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env", "hackclub").Run();

        _selectMessages.ShouldBeEmpty();
        _calls.Count.ShouldBe(1);
        _calls[0].Body["team"]!.ToString().ShouldBe("hackclub");
    }

    /// <remarks>Refer to "ArmorPull prompts for team and retries when api requires team"</remarks>
    [Fact]
    public void TestArmorPullPromptsForTeamAndRetriesWhenApiRequiresTeam()
    {
        Env(".env");
        Select("acme");
        Api((_, body) => body["team"] is null
            ? (400, TeamRequired("acme", "globex"))
            : (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorPull("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBe(["Select team"]);
        _calls[1].Body["team"]!.ToString().ShouldBe("acme");
    }

    /// <remarks>Refer to "ArmorPull uses only team from required error meta without prompting"</remarks>
    [Fact]
    public void TestArmorPullUsesOnlyTeamFromRequiredErrorMetaWithoutPrompting()
    {
        Env(".env");
        Api((_, body) => body["team"] is null
            ? (400, TeamRequired("solo"))
            : (200, """{"private_key":"priv-from-api","changed":true}"""));

        new ArmorPull("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBeEmpty();
        _calls[1].Body["team"]!.ToString().ShouldBe("solo");
    }

    /// <remarks>Refer to "ArmorPull rethrows non-team-required api errors"</remarks>
    [Fact]
    public void TestArmorPullRethrowsNonTeamRequiredApiErrors()
    {
        Env(".env");
        Api((_, _) => (500, """{"error":{"code":"BOOM","message":"boom"}}"""));

        Should.Throw<ApiException>(() => new ArmorPull("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run())
            .Code.ShouldBe("BOOM");
    }

    // ---- armorPush ----

    /// <remarks>Refer to "ArmorPush reads private key and posts push request without prompting first"</remarks>
    [Fact]
    public void TestArmorPushReadsPrivateKeyAndPostsPushRequestWithoutPromptingFirst()
    {
        (string publicKey, string privateKey) = Primitives.Primitives.Keypair();
        Keys(".env", privateKey);
        Api((_, _) => (200, """{"changed":true,"team":"dotenvx"}"""));

        ArmorResult result = new ArmorPush("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBeEmpty();
        _calls[0].Body["private_key"]!.ToString().ShouldBe(privateKey);
        result.PublicKeyValue.ShouldBe(publicKey);
        result.Changed.ShouldBeTrue();
    }

    /// <remarks>Refer to "ArmorPush prompts for team and retries when api requires team"</remarks>
    [Fact]
    public void TestArmorPushPromptsForTeamAndRetriesWhenApiRequiresTeam()
    {
        Keys(".env", Primitives.Primitives.Keypair().PrivateKey);
        Select("globex");
        Api((_, body) => body["team"] is null ? (400, TeamRequired("acme", "globex")) : (200, """{"changed":true}"""));

        new ArmorPush("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBe(["Select team"]);
        _calls[1].Body["team"]!.ToString().ShouldBe("globex");
    }

    /// <remarks>Refer to "ArmorPush sends explicit team without prompt or retry"</remarks>
    [Fact]
    public void TestArmorPushSendsExplicitTeamWithoutPromptOrRetry()
    {
        Keys(".env", Primitives.Primitives.Keypair().PrivateKey);
        Api((_, _) => (200, """{"changed":true}"""));

        new ArmorPush("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env", "hackclub").Run();

        _selectMessages.ShouldBeEmpty();
        _calls.Count.ShouldBe(1);
        _calls[0].Body["team"]!.ToString().ShouldBe("hackclub");
    }

    /// <remarks>Refer to "ArmorPush uses only team from required error meta without prompting"</remarks>
    [Fact]
    public void TestArmorPushUsesOnlyTeamFromRequiredErrorMetaWithoutPrompting()
    {
        Keys(".env", Primitives.Primitives.Keypair().PrivateKey);
        Api((_, body) => body["team"] is null ? (400, TeamRequired("solo")) : (200, """{"changed":true}"""));

        new ArmorPush("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run();

        _selectMessages.ShouldBeEmpty();
        _calls[1].Body["team"]!.ToString().ShouldBe("solo");
    }

    /// <remarks>Refer to "ArmorPush rethrows non-team-required api errors"</remarks>
    [Fact]
    public void TestArmorPushRethrowsNonTeamRequiredApiErrors()
    {
        Keys(".env", Primitives.Primitives.Keypair().PrivateKey);
        Api((_, _) => (500, """{"error":{"code":"BOOM","message":"boom"}}"""));

        Should.Throw<ApiException>(() => new ArmorPush("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run())
            .Code.ShouldBe("BOOM");
    }

    // ---- armorMove ----

    /// <remarks>Refer to "ArmorMove reads public key, prompts for team, and posts move request"</remarks>
    [Fact]
    public void TestArmorMoveReadsPublicKeyPromptsForTeamAndPostsMoveRequest()
    {
        Env(".env.production");
        Select("hackclub");
        Api((path, _) => path == "/api/account"
            ? (200, """{"organizations":[{"provider_slug":"dotenvx"},{"provider_slug":"hackclub"}]}""")
            : (200, """{"changed":true,"team":"hackclub","private_key":"priv-from-api"}"""));

        ArmorResult result = new ArmorMove("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env.production").Run();

        _selectMessages.ShouldBe(["Select team"]);
        _calls[1].Path.ShouldBe("/api/armor/move");
        _calls[1].Body["team"]!.ToString().ShouldBe("hackclub");
        _calls[1].Body["public_key"]!.ToString().ShouldBe(Pub);
        result.ShouldBe(new ArmorResult(true, "DOTENV_PRIVATE_KEY_PRODUCTION", "priv-from-api", Pub, "hackclub"));
    }

    /// <remarks>Refer to "ArmorMove returns unchanged when api reports no change"</remarks>
    [Fact]
    public void TestArmorMoveReturnsUnchangedWhenApiReportsNoChange()
    {
        Env(".env");
        Select("dotenvx");
        Api((path, _) => path == "/api/account"
            ? (200, """{"organizations":[{"provider_slug":"dotenvx"}]}""")
            : (200, """{"changed":false,"team":"dotenvx"}"""));

        new ArmorMove("https://armor.dotenvx.com", "token-1", "device-pub-1", ".env").Run().Changed.ShouldBeFalse();
    }

    // ---- login / logout ----

    private sealed class SessionSpy : Session
    {
        public List<string[]> Logins { get; } = [];
        public List<string?[]> Logouts { get; } = [];
        public int Updates { get; private set; }
        public string? TokenValue { get; init; }

        public override string DevicePublicKey() => "device-pub";
        public override SystemInformation.Info SystemInformation() => new("uuid", "darwin", "arm64");
        public override string? Token() => TokenValue;

        public override string Login(string? hostname, string? id, string? username, string? accessToken)
        {
            Logins.Add([hostname!, id!, username!, accessToken!]);

            return accessToken!;
        }

        public override void NotifyUpdate() => Updates++;

        public override bool Logout(string? hostname, string? id, string? accessToken)
        {
            Logouts.Add([hostname, id, accessToken]);

            return true;
        }
    }

    /// <remarks>Refer to "Login requests device code with device public key and system info"</remarks>
    [Fact]
    public void TestLoginRequestsDeviceCodeWithDevicePublicKeyAndSystemInfo()
    {
        Api((_, _) => (200, """{"device_code":"device-code","user_code":"user-code","verification_uri":"https://armor.example.com/device","verification_uri_complete":"https://armor.example.com/device?code=user-code","interval":3}"""));

        LoginStart start = new Login("https://armor.example.com", new SessionSpy()).Run();

        _calls[0].Body["device_public_key"]!.ToString().ShouldBe("device-pub");
        _calls[0].Body["system_information"]!["system_uuid"]!.ToString().ShouldBe("uuid");
        start.ShouldBe(new LoginStart("device-code", "user-code", "https://armor.example.com/device",
            "https://armor.example.com/device?code=user-code", 3));
    }

    /// <remarks>Refer to "Login poll saves token when access token is returned"</remarks>
    [Fact]
    public void TestLoginPollSavesTokenWhenAccessTokenIsReturned()
    {
        var session = new SessionSpy();
        Api((_, _) => (200, """{"access_token":"token-123","id":"user-id","username":"scott"}"""));

        JsonNode result = new Login("https://armor.example.com", session).Poll("device-code", 0);

        result["access_token"]!.ToString().ShouldBe("token-123");
        session.Logins.Single().ShouldBe(["https://armor.example.com", "user-id", "scott", "token-123"]);
        session.Updates.ShouldBe(1);
    }

    /// <remarks>Refer to "Login poll continues on authorization_pending"</remarks>
    [Fact]
    public void TestLoginPollContinuesOnAuthorizationPending()
    {
        var session = new SessionSpy();
        Api((_, _) => _calls.Count == 1
            ? (400, """{"error":"authorization_pending","error_description":"still waiting"}""")
            : (200, """{"access_token":"token-123","id":"user-id","username":"scott"}"""));

        JsonNode result = new Login("https://armor.example.com", session).Poll("device-code", 0);

        result["access_token"]!.ToString().ShouldBe("token-123");
        _calls.Count.ShouldBe(2);
        session.Updates.ShouldBe(1);
    }

    /// <remarks>Refer to "Login poll waits when token response has no access token yet"</remarks>
    [Fact]
    public void TestLoginPollWaitsWhenTokenResponseHasNoAccessTokenYet()
    {
        var session = new SessionSpy();
        Api((_, _) => _calls.Count == 1
            ? (200, "{}")
            : (200, """{"access_token":"token-123","id":"user-id","username":"scott"}"""));

        JsonNode result = new Login("https://armor.example.com", session).Poll("device-code", 0);

        result["access_token"]!.ToString().ShouldBe("token-123");
        _calls.Count.ShouldBe(2);
        session.Updates.ShouldBe(1);
    }

    /// <remarks>Refer to "Login poll throws non-pending errors"</remarks>
    [Fact]
    public void TestLoginPollThrowsNonPendingErrors()
    {
        Api((_, _) => (500, """{"error":"server_error","error_description":"boom"}"""));

        Should.Throw<ApiException>(() => new Login("https://armor.example.com", new SessionSpy()).Poll("device-code", 0))
            .Code.ShouldBe("server_error");
    }

    /// <remarks>Refer to "Logout posts logout and clears local session"</remarks>
    [Fact]
    public void TestLogoutPostsLogoutAndClearsLocalSession()
    {
        var session = new SessionSpy { TokenValue = "token-123" };
        string? authorization = null;
        ArmorHttp.Request = (_, _, headers, _) =>
        {
            authorization = headers!["Authorization"];

            return new HttpResult(200, """{"id":"user-id","username":"scott","access_token":"token-123"}""");
        };

        LogoutResult result = new Logout("https://armor.example.com", session).Run();

        authorization.ShouldBe("Bearer token-123");
        session.Logouts.Single().ShouldBe(["https://armor.example.com", "user-id", "token-123"]);
        result.ShouldBe(new LogoutResult("scott", "token-123", "https://armor.example.com/settings/devices"));
    }

    /// <remarks>Refer to "Logout does not clear local session when api logout fails"</remarks>
    [Fact]
    public void TestLogoutDoesNotClearLocalSessionWhenApiLogoutFails()
    {
        var session = new SessionSpy { TokenValue = "token-123" };
        Api((_, _) => (500, """{"error":{"code":"BOOM","message":"logout failed"}}"""));

        Should.Throw<ApiException>(() => new Logout("https://armor.example.com", session).Run());

        session.Logouts.ShouldBeEmpty();
    }
}
