using System.CommandLine;
using System.Text.Json.Nodes;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/armor/{up,down,pull,push,move}.js, cli/actions/{curl,login,logout}.js and
/// cli/commands/armor.js. Upstream proxyquires the Session and the services; here the config directory holds a real
/// session (hostname/token in its settings file), the HTTP seam stands in for Armor and the files in the working directory
/// are the project, so each assertion is about requests sent, messages logged and exit codes returned. A few upstream
/// scenarios are JavaScript-only ("non-error rejection values", `_actionHandler` plumbing) and are noted at their tests.
/// </remarks>
public class ArmorActionsTest : EnvironmentTestBase
{
    private const string Hostname = "https://armor.example.com";
    private readonly List<(string Url, string Method, IReadOnlyDictionary<string, string>? Headers, string? Body)> _requests = [];
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-armor-actions-" + Guid.NewGuid().ToString("N"));

    public ArmorActionsTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
        PackageJson.Version = "0.0.0-test";

        string config = Environment.GetEnvironmentVariable("DOTENVX_CONFIG")!;
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ".env"), string.Join("\n",
            $"DOTENVX_ARMOR_HOSTNAME=\"{Hostname}\"", "DOTENVX_ARMOR_USERNAME=\"scott\"", "DOTENVX_ARMOR_TOKEN=\"token-123\"",
            "DOTENVX_ARMOR_ON=\"true\"", "DOTENVX_VERSION_LAST_CHECK=\"99999999999999\""));
    }

    protected override void TearDown()
    {
        base.TearDown();
        Environment.CurrentDirectory = Path.GetTempPath();
        Directory.Delete(_dir, true);
    }

    private void Respond(Func<string, string?, (int Status, string Body)> handler)
    {
        ArmorHttp.Request = (url, method, headers, body) =>
        {
            _requests.Add((url, method, headers, body));
            (int status, string text) = handler(new Uri(url).AbsolutePath, body);

            return new HttpResult(status, text);
        };
    }

    private int Run(params string[] args)
    {
        RootCommand root = new LoggingRootCommand
        {
            new ArmorCommand(MockLogger), new LoginAction(MockLogger), new LogoutAction(MockLogger),
            new CurlAction(MockLogger)
        };

        return root.Parse(args).Invoke();
    }

    private static readonly (string PublicKey, string PrivateKey) Pair = global::Dotenvx.Primitives.Primitives.Keypair();

    private static string PairDisplay => ArmoredKeyDisplay.Display(Pair.PublicKey);

    private static void EnvFiles(string file = ".env", string? publicKey = null)
    {
        publicKey ??= Pair.PublicKey;
        string name = file == ".env" ? "DOTENV_PUBLIC_KEY" : "DOTENV_PUBLIC_KEY_" + file[5..].ToUpperInvariant();
        File.WriteAllText(file, $"{name}=\"{publicKey}\"\n");
    }

    private static void KeysFile(string privateKey, string file = ".env")
    {
        string name = file == ".env" ? "DOTENV_PRIVATE_KEY" : "DOTENV_PRIVATE_KEY_" + file[5..].ToUpperInvariant();
        File.WriteAllText(".env.keys", $"{name}=\"{privateKey}\"\n");
    }

    private string RequestToken(int index = 0) => _requests[index].Headers!["Authorization"];

    private JsonNode RequestBody(int index = 0) => JsonNode.Parse(_requests[index].Body!)!;

    // ---- armor up ----

    /// <remarks>Refer to "armor up passes explicit team option to ArmorUp service"</remarks>
    [Fact]
    public void TestArmorUpPassesExplicitTeamOptionToArmorUpService()
    {
        EnvFiles();
        KeysFile(Pair.PrivateKey);
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "up", "--team", "hackclub").ShouldBe(0);

        RequestBody()["team"]!.ToString().ShouldBe("hackclub");
        _requests[0].Url.ShouldBe($"{Hostname}/api/armor/up");
        RequestToken().ShouldBe("Bearer token-123");
        MockLogger.InfoMessages.ShouldContain($"⛨ armored .env ({PairDisplay})");
    }

    /// <remarks>Refer to "armor up prints no changes message when remote armor is unchanged"</remarks>
    [Fact]
    public void TestArmorUpPrintsNoChangesMessageWhenRemoteArmorIsUnchanged()
    {
        EnvFiles();
        Respond((_, _) => (200, """{"changed":false,"private_key":"p"}"""));

        Run("armor", "up").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain($"○ no change to .env ({PairDisplay})");
    }

    /// <remarks>Refer to "armor up logs errors and exits" and "armor up logs errors and returns failure"</remarks>
    [Fact]
    public void TestArmorUpLogsErrorsAndExits()
    {
        EnvFiles();
        KeysFile(Pair.PrivateKey);
        Respond((_, _) => (401, """{"error":{"code":"UNAUTHORIZED","message":"bad token"}}"""));

        Run("armor", "up").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("[UNAUTHORIZED] bad token");
    }

    // ---- armor down ----

    /// <remarks>Refer to "armor down uses session values and calls ArmorDown service with default env file"</remarks>
    [Fact]
    public void TestArmorDownUsesSessionValuesAndCallsArmorDownServiceWithDefaultEnvFile()
    {
        EnvFiles();
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "down").ShouldBe(0);

        _requests[0].Url.ShouldBe($"{Hostname}/api/armor/down");
        RequestToken().ShouldBe("Bearer token-123");
        RequestBody()["public_key"]!.ToString().ShouldBe(Pair.PublicKey);
        MockLogger.InfoMessages.ShouldContain($"◇ dearmored .env ({PairDisplay}) to .env.keys");
    }

    /// <remarks>Refer to "armor down passes explicit env file option to ArmorDown service"</remarks>
    [Fact]
    public void TestArmorDownPassesExplicitEnvFileOptionToArmorDownService()
    {
        EnvFiles(".env.production", "03aaaaaaaa");
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "down", "-f", ".env.production").ShouldBe(0);

        RequestBody()["public_key"]!.ToString().ShouldBe("03aaaaaaaa");
        File.ReadAllText(".env.keys").ShouldContain("DOTENV_PRIVATE_KEY_PRODUCTION=");
    }

    /// <remarks>Refer to "armor down passes explicit team option to ArmorDown service"</remarks>
    [Fact]
    public void TestArmorDownPassesExplicitTeamOptionToArmorDownService()
    {
        EnvFiles();
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "down", "--team", "hackclub").ShouldBe(0);

        RequestBody()["team"]!.ToString().ShouldBe("hackclub");
    }

    /// <remarks>Refer to "armor down prints no changes message when remote armor is unchanged"</remarks>
    [Fact]
    public void TestArmorDownPrintsNoChangesMessageWhenRemoteArmorIsUnchanged()
    {
        EnvFiles();
        Respond((_, _) => (200, """{"changed":false,"private_key":"p"}"""));

        Run("armor", "down").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain($"○ no change to .env ({PairDisplay})");
    }

    /// <remarks>Refer to "armor down logs errors and exits" and "armor down logs errors and returns failure"</remarks>
    [Fact]
    public void TestArmorDownLogsErrorsAndExits()
    {
        EnvFiles();
        Respond((_, _) => (500, """{"error":{"code":"BOOM","message":"down failed"}}"""));

        Run("armor", "down").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("[BOOM] down failed");
    }

    // ---- armor pull ----

    /// <remarks>Refer to "armor pull uses session values and calls ArmorPull service with default env file"</remarks>
    [Fact]
    public void TestArmorPullUsesSessionValuesAndCallsArmorPullServiceWithDefaultEnvFile()
    {
        EnvFiles();
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "pull").ShouldBe(0);

        _requests[0].Url.ShouldBe($"{Hostname}/api/armor/pull");
        RequestToken().ShouldBe("Bearer token-123");
        MockLogger.InfoMessages.ShouldContain($"◇ pulled .env ({PairDisplay}) to .env.keys");
    }

    /// <remarks>Refer to "armor pull passes explicit env file option to ArmorPull service"</remarks>
    [Fact]
    public void TestArmorPullPassesExplicitEnvFileOptionToArmorPullService()
    {
        EnvFiles(".env.production", "03aaaaaaaa");
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "pull", "-f", ".env.production").ShouldBe(0);

        RequestBody()["public_key"]!.ToString().ShouldBe("03aaaaaaaa");
    }

    /// <remarks>Refer to "armor pull passes explicit team option to ArmorPull service"</remarks>
    [Fact]
    public void TestArmorPullPassesExplicitTeamOptionToArmorPullService()
    {
        EnvFiles();
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "pull", "--team", "hackclub").ShouldBe(0);

        RequestBody()["team"]!.ToString().ShouldBe("hackclub");
    }

    /// <remarks>Refer to "armor pull prints no changes message when .env.keys is unchanged"</remarks>
    [Fact]
    public void TestArmorPullPrintsNoChangesMessageWhenEnvKeysIsUnchanged()
    {
        EnvFiles();
        File.WriteAllText(".env.keys", "DOTENV_PRIVATE_KEY=p\n");
        Respond((_, _) => (200, """{"changed":true,"private_key":"p"}"""));

        Run("armor", "pull").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain($"○ no change to .env ({PairDisplay})");
    }

    /// <remarks>Refer to "armor pull logs errors and exits" and "armor pull logs errors and returns failure"</remarks>
    [Fact]
    public void TestArmorPullLogsErrorsAndExits()
    {
        EnvFiles();
        Respond((_, _) => (500, """{"error":{"code":"BOOM","message":"pull failed"}}"""));

        Run("armor", "pull").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("[BOOM] pull failed");
    }

    // ---- armor push ----

    /// <remarks>Refer to "armor push passes explicit team option to ArmorPush service"</remarks>
    [Fact]
    public void TestArmorPushPassesExplicitTeamOptionToArmorPushService()
    {
        (string publicKey, string privateKey) = global::Dotenvx.Primitives.Primitives.Keypair();
        KeysFile(privateKey);
        Respond((_, _) => (200, """{"changed":true}"""));

        Run("armor", "push", "--team", "hackclub").ShouldBe(0);

        RequestBody()["team"]!.ToString().ShouldBe("hackclub");
        MockLogger.InfoMessages.ShouldContain($"⛨ pushed .env ({ArmoredKeyDisplay.Display(publicKey)}) to Armor");
    }

    /// <remarks>Refer to "armor push prints no changes message when remote armor is unchanged"</remarks>
    [Fact]
    public void TestArmorPushPrintsNoChangesMessageWhenRemoteArmorIsUnchanged()
    {
        (string publicKey, string privateKey) = global::Dotenvx.Primitives.Primitives.Keypair();
        KeysFile(privateKey);
        Respond((_, _) => (200, """{"changed":false}"""));

        Run("armor", "push").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain($"○ no change to .env ({ArmoredKeyDisplay.Display(publicKey)})");
    }

    /// <remarks>Refer to "armor push logs errors and exits" and "armor push logs errors and returns failure"</remarks>
    [Fact]
    public void TestArmorPushLogsErrorsAndExits()
    {
        KeysFile(Pair.PrivateKey);
        Respond((_, _) => (500, """{"error":{"code":"BOOM","message":"push failed"}}"""));

        Run("armor", "push").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("[BOOM] push failed");
    }

    // ---- armor move ----

    private void MoveApi(string moveJson)
    {
        Prompts.Select = _ => "hackclub";
        Respond((path, _) => path == "/api/account"
            ? (200, """{"organizations":[{"provider_slug":"hackclub"}]}""")
            : (200, moveJson));
    }

    /// <remarks>Refer to "armor move uses session values and calls ArmorMove service with default env file"</remarks>
    [Fact]
    public void TestArmorMoveUsesSessionValuesAndCallsArmorMoveServiceWithDefaultEnvFile()
    {
        EnvFiles();
        MoveApi("""{"changed":true,"team":"hackclub","private_key":"p"}""");

        Run("armor", "move").ShouldBe(0);

        _requests[0].Url.ShouldBe($"{Hostname}/api/account");
        _requests[1].Url.ShouldBe($"{Hostname}/api/armor/move");
        MockLogger.InfoMessages.ShouldContain($"⛨ moved to hackclub ({PairDisplay})");
    }

    /// <remarks>Refer to "armor move passes explicit env file option to ArmorMove service"</remarks>
    [Fact]
    public void TestArmorMovePassesExplicitEnvFileOptionToArmorMoveService()
    {
        EnvFiles(".env.production", "03aaaaaaaa");
        MoveApi("""{"changed":true,"team":"hackclub","private_key":"p"}""");

        Run("armor", "move", "-f", ".env.production").ShouldBe(0);

        RequestBody(1)["public_key"]!.ToString().ShouldBe("03aaaaaaaa");
    }

    /// <remarks>Refer to "armor move prints no changes message when remote team is unchanged"</remarks>
    [Fact]
    public void TestArmorMovePrintsNoChangesMessageWhenRemoteTeamIsUnchanged()
    {
        EnvFiles();
        MoveApi("""{"changed":false,"team":"hackclub"}""");

        Run("armor", "move").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain($"○ no change ({PairDisplay})");
    }

    /// <remarks>Refer to "armor move logs errors and exits" and "armor move logs errors and returns failure"</remarks>
    [Fact]
    public void TestArmorMoveLogsErrorsAndExits()
    {
        EnvFiles();
        Respond((_, _) => (500, """{"error":{"code":"BOOM","message":"move failed"}}"""));

        Run("armor", "move").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("[BOOM] move failed");
    }

    // ---- curl ----

    private string CaptureOutput(Func<int> run, out int exit)
    {
        var output = new StringWriter { NewLine = "\n" };
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            exit = run();
        }
        finally
        {
            Console.SetOut(original);
        }

        return output.ToString();
    }

    /// <remarks>Refer to "makes an authenticated GET request and pretty prints JSON"</remarks>
    [Fact]
    public void TestMakesAnAuthenticatedGetRequestAndPrettyPrintsJson()
    {
        Respond((_, _) => (200, """{"keypairs":[]}"""));

        string output = CaptureOutput(() => Run("curl", $"{Hostname}/api/armor/keypairs"), out int exit);

        exit.ShouldBe(0);
        _requests[0].Url.ShouldBe($"{Hostname}/api/armor/keypairs");
        _requests[0].Method.ShouldBe("GET");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer token-123");
        _requests[0].Headers!["Accept"].ShouldBe("application/json");
        _requests[0].Headers!["dotenvx-device-public-key"].ShouldNotBeNullOrEmpty();
        _requests[0].Body.ShouldBeNull();
        output.ShouldBe("{\n  \"keypairs\": []\n}\n");
    }

    /// <remarks>Refer to "defaults to POST when JSON data is supplied"</remarks>
    [Fact]
    public void TestDefaultsToPostWhenJsonDataIsSupplied()
    {
        Respond((_, _) => (200, "{}"));

        CaptureOutput(() => Run("curl", $"{Hostname}/api/armor/keypairs/public-key/settings/guard", "--data", """{ "value": true }"""),
            out _);

        _requests[0].Method.ShouldBe("POST");
        _requests[0].Body.ShouldBe("""{"value":true}""");
        _requests[0].Headers!["Content-Type"].ShouldBe("application/json");
    }

    /// <remarks>Refer to "supports an explicit request method and token"</remarks>
    [Fact]
    public void TestSupportsAnExplicitRequestMethodAndToken()
    {
        Respond((_, _) => (200, "{}"));

        CaptureOutput(() => Run("curl", $"{Hostname}/api/armor/keypairs", "-X", "patch", "--token", "token-xyz"), out _);

        _requests[0].Method.ShouldBe("PATCH");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer token-xyz");
    }

    /// <remarks>Refer to "refuses to send the token to another origin"</remarks>
    [Fact]
    public void TestRefusesToSendTheTokenToAnotherOrigin()
    {
        Respond((_, _) => (200, "{}"));

        Run("curl", "https://evil.example/api/armor/keypairs").ShouldBe(1);

        _requests.ShouldBeEmpty();
        MockLogger.ErrorMessages.ShouldContain(
            "refusing to send Armor token to [https://evil.example]. expected [https://armor.example.com]");
    }

    /// <remarks>Refer to "prints API error JSON and sets a failing exit code" and "prints API error JSON and returns a failing exit code"</remarks>
    [Fact]
    public void TestPrintsApiErrorJsonAndSetsAFailingExitCode()
    {
        Respond((_, _) => (401, """{"error":{"code":"UNAUTHORIZED"}}"""));

        string output = CaptureOutput(() => Run("curl", $"{Hostname}/api/armor/keypairs"), out int exit);

        output.ShouldContain("UNAUTHORIZED");
        exit.ShouldBe(1);
    }

    // ---- login / logout ----

    /// <remarks>Refer to "login action runs native oauth device flow"</remarks>
    [Fact]
    public void TestLoginActionRunsNativeOauthDeviceFlow()
    {
        Respond((path, _) => path == "/oauth/device/code"
            ? (200, """{"device_code":"device-code","user_code":"ABCD1234","verification_uri":"https://armor.example.com/device","verification_uri_complete":"https://armor.example.com/device?code=ABCD1234","interval":0}""")
            : (200, """{"access_token":"token-new","id":"user-id","username":"scott"}"""));

        Run("login", "--hostname", Hostname).ShouldBe(0);

        _requests[0].Url.ShouldBe($"{Hostname}/oauth/device/code");
        MockLogger.InfoMessages.ShouldContain(
            "◌ press Enter to open [https://armor.example.com/device] and enter code [ABCD-1234]...");
        MockLogger.InfoMessages.ShouldContain("◉ logged in (scott)");
    }

    /// <remarks>Refer to "login action falls back to saved hostname and reports errors"</remarks>
    [Fact]
    public void TestLoginActionFallsBackToSavedHostnameAndReportsErrors()
    {
        Respond((_, _) => (400, """{"error":"invalid_request","error_description":"login failed"}"""));

        Run("login").ShouldBe(1);

        _requests[0].Url.ShouldBe($"{Hostname}/oauth/device/code");
        MockLogger.ErrorMessages.ShouldContain("[invalid_request] login failed");
    }

    /// <remarks>
    /// Refer to "login action reports non-error rejection values". JavaScript can reject with a bare string; .NET
    /// exceptions always carry a message, so the nearest equivalent (a transport failure) is reported by its message.
    /// </remarks>
    [Fact]
    public void TestLoginActionReportsNonErrorRejectionValues()
    {
        ArmorHttp.Request = (_, _, _, _) => throw new HttpRequestFailedException("ECONNREFUSED", "boom");

        Run("login").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("boom");
    }

    /// <remarks>Refer to "logout action runs native logout service"</remarks>
    [Fact]
    public void TestLogoutActionRunsNativeLogoutService()
    {
        Respond((_, _) => (200, """{"id":"user-id","username":"scott","access_token":"token-123"}"""));

        Run("logout", "--hostname", Hostname).ShouldBe(0);

        _requests[0].Url.ShouldBe($"{Hostname}/api/logout");
        MockLogger.InfoMessages.ShouldContain("◌ logged out (scott)");
    }

    /// <remarks>Refer to "logout action falls back to saved hostname and reports errors"</remarks>
    [Fact]
    public void TestLogoutActionFallsBackToSavedHostnameAndReportsErrors()
    {
        Respond((_, _) => (500, """{"error":{"code":"BOOM","message":"logout failed"}}"""));

        Run("logout").ShouldBe(1);

        _requests[0].Url.ShouldBe($"{Hostname}/api/logout");
        MockLogger.ErrorMessages.ShouldContain("[BOOM] logout failed");
    }

    /// <remarks>Refer to "logout action reports non-error rejection values" (see the login counterpart's note)</remarks>
    [Fact]
    public void TestLogoutActionReportsNonErrorRejectionValues()
    {
        ArmorHttp.Request = (_, _, _, _) => throw new HttpRequestFailedException("ECONNREFUSED", "boom");

        Run("logout").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain("boom");
    }

    // ---- commands/armor.js ----

    private static Command ArmorGroup(RootCommand root)
    {
        return root.Subcommands.Single(command => command.Name == "armor");
    }

    private RootCommand Root()
    {
        return new LoggingRootCommand { new ArmorCommand(MockLogger) };
    }

    /// <remarks>Refer to "armor subcommands accept explicit token option"</remarks>
    [Fact]
    public void TestArmorSubcommandsAcceptExplicitTokenOption()
    {
        Command armor = ArmorGroup(Root());

        foreach (string name in new[] { "up", "down", "push", "pull", "move" })
        {
            Command command = armor.Subcommands.Single(c => c.Name == name);
            command.Options.Any(option => option.Name == "--token").ShouldBeTrue($"armor {name} declares --token");
        }
    }

    /// <remarks>
    /// Refer to "armor commands are native cli subcommands". The root-help lines ("curated armor line") are rendered by
    /// each CLI's own help builder and compared by HelpTextParityTest; here the subcommand set and its order are checked.
    /// </remarks>
    [Fact]
    public void TestArmorCommandsAreNativeCliSubcommands()
    {
        ArmorGroup(Root()).Subcommands.Select(command => command.Name).ShouldBe(
            ["up", "down", "push", "pull", "open", "move", "login", "logout", "status", "settings"]);
    }

    /// <remarks>Refer to "armor login and logout resolve through native actions"</remarks>
    [Fact]
    public void TestArmorLoginAndLogoutResolveThroughNativeActions()
    {
        Command armor = ArmorGroup(Root());

        armor.Subcommands.Single(c => c.Name == "login").Options.Any(o => o.Name == "--hostname").ShouldBeTrue();
        armor.Subcommands.Single(c => c.Name == "logout").Options.Any(o => o.Name == "--hostname").ShouldBeTrue();

        Respond((_, _) => (200, """{"id":"user-id","username":"scott","access_token":"token-123"}"""));
        Run("armor", "logout").ShouldBe(0);
        _requests.Single().Url.ShouldBe($"{Hostname}/api/logout");
    }

    /// <remarks>Refer to "armor status resolves through native action"</remarks>
    [Fact]
    public void TestArmorStatusResolvesThroughNativeAction()
    {
        string output = CaptureOutput(() => Run("armor", "status"), out int exit);

        exit.ShouldBe(0);
        output.Trim().ShouldBe("on");
    }

    /// <remarks>Refer to "armor settings is an embedded native command"</remarks>
    [Fact]
    public void TestArmorSettingsIsAnEmbeddedNativeCommand()
    {
        Command settings = ArmorGroup(Root()).Subcommands.Single(c => c.Name == "settings");

        settings.Subcommands.Select(c => c.Name).ShouldBe(["username", "token", "device", "hostname", "path", "on", "off"]);
        CaptureOutput(() => Run("armor", "settings", "username"), out _).Trim().ShouldBe("scott");
    }

    /// <remarks>Refer to "armor default action shows help"</remarks>
    [Fact]
    public void TestArmorDefaultActionShowsHelp()
    {
        string output = CaptureOutput(() => Run("armor"), out int exit);

        exit.ShouldBe(0);
        output.ShouldContain("up");
        output.ShouldContain("settings");
    }

    /// <remarks>Refer to "armor unknown subcommands fall back to dotenvx-armor"</remarks>
    [Fact]
    public void TestArmorUnknownSubcommandsFallBackToDotenvxArmor()
    {
        var spawned = new List<(string Command, string[] Args)>();
        ExecuteDynamic.SpawnCommand = (command, args, _) =>
        {
            spawned.Add((command, args));

            return new SpawnResult(0, false);
        };

        Run("armor", "keypair", "--flag").ShouldBe(0);

        spawned.Single().Command.ShouldBe("dotenvx-armor");
        spawned.Single().Args.ShouldBe(["keypair", "--flag"]);
    }

    /// <remarks>Refer to "armor keypair is treated as an unknown armor command"</remarks>
    [Fact]
    public void TestArmorKeypairIsTreatedAsAnUnknownArmorCommand()
    {
        ArmorGroup(Root()).Subcommands.Any(c => c.Name == "keypair").ShouldBeFalse("armor keypair is not registered");
    }

    // The two upstream "falls back to private key name when public key display is unavailable" tests stub the service to return a
    // null public key; ArmorUp.Factory / ArmorPush.Factory are the seams that let the action see the same result here.
    /// <remarks>Refer to "armor up falls back to private key name when public key display is unavailable"</remarks>
    [Fact]
    public void ArmorUpFallsBackToPrivateKeyNameWhenPublicKeyDisplayIsUnavailable()
    {
        var service = NSubstitute.Substitute.For<global::Dotenvx.Interop.DotenvxJs.Services.ArmorUp>("h", null, "d", null, null);
        service.Run().Returns(new global::Dotenvx.Interop.DotenvxJs.Services.ArmorResult(true, "DOTENV_PRIVATE_KEY", null, null));
        global::Dotenvx.Interop.DotenvxJs.Services.ArmorUp.Factory = (_, _, _, _, _) => service;

        Run("armor", "up").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain("⛨ armored .env (DOTENV_PRIVATE_KEY)");
    }

    /// <remarks>Refer to "armor push falls back to private key name when public key display is unavailable"</remarks>
    [Fact]
    public void ArmorPushFallsBackToPrivateKeyNameWhenPublicKeyDisplayIsUnavailable()
    {
        var service = NSubstitute.Substitute.For<global::Dotenvx.Interop.DotenvxJs.Services.ArmorPush>("h", null, "d", null, null);
        service.Run().Returns(new global::Dotenvx.Interop.DotenvxJs.Services.ArmorResult(true, "DOTENV_PRIVATE_KEY", null, null));
        global::Dotenvx.Interop.DotenvxJs.Services.ArmorPush.Factory = (_, _, _, _, _) => service;

        Run("armor", "push").ShouldBe(0);

        MockLogger.InfoMessages.ShouldContain("⛨ pushed .env (DOTENV_PRIVATE_KEY) to Armor");
    }
}
