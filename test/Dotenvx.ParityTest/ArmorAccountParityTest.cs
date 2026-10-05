using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test (upstream's armor/login/logout/curl/settings tests mock the API client and the session) -
/// end-to-end comparison of `armor status`, `armor settings *`, `login`, `logout` and `curl` against a fake Armor API
/// shared by the Node and .NET runs. See <see cref="ArmorParityTestBase" />.
/// </remarks>
public class ArmorAccountParityTest(NodeParityFixture fixture) : ArmorParityTestBase(fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusMatchesNode(bool loggedIn)
    {
        if (!Available)
        {
            return;
        }

        if (loggedIn)
        {
            LoggedIn();
        }

        Outcome outcome = await Compare("armor", "status");

        outcome.Stdout.Trim().ShouldBe(loggedIn ? "on" : "off");
    }

    [Fact]
    public async Task StatusIsOffWhenTurnedOff()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn(on: false);

        (await Compare("armor", "status")).Stdout.Trim().ShouldBe("off");
    }

    [Fact]
    public async Task StatusIsOffWithoutAToken()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn(token: null);

        (await Compare("armor", "status")).Stdout.Trim().ShouldBe("off");
    }

    [Theory]
    [InlineData("username")]
    [InlineData("token")]
    [InlineData("token", "--unmask")]
    [InlineData("device")]
    [InlineData("device", "--unmask")]
    [InlineData("hostname")]
    [InlineData("path")]
    public async Task SettingsReadersMatchNodeWhenLoggedIn(params string[] settingsArgs)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();

        await Compare(["armor", "settings", .. settingsArgs]);
    }

    [Theory]
    [InlineData("username")]
    [InlineData("token")]
    [InlineData("hostname")]
    [InlineData("path")]
    [InlineData("device")]
    public async Task SettingsReadersMatchNodeWhenLoggedOut(string setting)
    {
        if (!Available)
        {
            return;
        }

        await Compare("armor", "settings", setting);
    }

    [Theory]
    [InlineData("on")]
    [InlineData("off")]
    public async Task SettingsOnOffMatchNode(string toggle)
    {
        if (!Available)
        {
            return;
        }

        await Compare("armor", "settings", toggle);
        LoggedIn();
        await Compare("armor", "settings", toggle);
    }

    [Fact]
    public async Task LoginStoresTheSessionLikeNode()
    {
        if (!Available)
        {
            return;
        }

        Server.Handler = request => request.Path switch
        {
            "/oauth/device/code" => new FakeResponse(200,
                """{"device_code":"dev123","user_code":"ABCD1234","verification_uri":"https://armor.example/device","verification_uri_complete":"https://armor.example/device?code=ABCD1234","interval":1}"""),
            "/oauth/token" => new FakeResponse(200,
                """{"access_token":"tok_new_123456","id":"usr_9","username":"bob"}"""),
            _ => new FakeResponse(404, "{}")
        };

        Outcome outcome = await CompareWith(null, null, null, "login", "--hostname", Server.BaseUrl);

        outcome.Stderr.ShouldContain("◉ logged in (bob)");
        outcome.Stdout.ShouldContain("press Enter to open [https://armor.example/device] and enter code [ABCD-1234]");
        outcome.Files["config/.env"].ShouldContain("DOTENVX_ARMOR_USERNAME=\"bob\"");
    }

    [Fact]
    public async Task LoginKeepsPollingWhileAuthorizationIsPending()
    {
        if (!Available)
        {
            return;
        }

        int polls = 0;
        Server.Handler = request =>
        {
            if (request.Path == "/oauth/device/code")
            {
                return new FakeResponse(200,
                    """{"device_code":"dev123","user_code":"WXYZ","verification_uri":"https://armor.example/device","verification_uri_complete":"https://armor.example/device?code=WXYZ","interval":1}""");
            }

            return Interlocked.Increment(ref polls) % 2 == 1
                ? new FakeResponse(400, """{"error":"authorization_pending","error_description":"pending"}""")
                : new FakeResponse(200, """{"access_token":"tok_new_123456","id":"usr_9","username":"bob"}""");
        };

        Outcome outcome = await CompareWith(null, null, null, "armor", "login", "--hostname", Server.BaseUrl);

        outcome.Requests.Count(r => r.Contains("/oauth/token")).ShouldBe(2);
    }

    [Fact]
    public async Task LoginOauthErrorMatchesNode()
    {
        if (!Available)
        {
            return;
        }

        Server.Handler = request => request.Path == "/oauth/device/code"
            ? new FakeResponse(200,
                """{"device_code":"dev123","user_code":"ABCD1234","verification_uri":"https://armor.example/device","verification_uri_complete":"https://armor.example/device?code=ABCD1234","interval":1}""")
            : new FakeResponse(400, """{"error":"access_denied","error_description":"user denied"}""");

        Outcome outcome = await CompareWith(null, null, null, "login", "--hostname", Server.BaseUrl);

        outcome.ExitCode.ShouldBe(1);
        outcome.Stderr.ShouldContain("[access_denied] user denied");
    }

    [Fact]
    public async Task LoginAgainstADeadServerReportsTheNetworkError()
    {
        if (!Available)
        {
            return;
        }

        string dead = Server.BaseUrl;
        Server.Dispose();

        Outcome outcome = await CompareWith(null, null, null, "login", "--hostname", dead);

        outcome.ExitCode.ShouldBe(1);
        outcome.Stderr.ShouldContain("ECONNREFUSED");
    }

    [Fact]
    public async Task LogoutMatchesNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200,
            """{"id":"usr_1","username":"alice","access_token":"tok_abc1234567890"}""");

        Outcome outcome = await Compare("logout");

        outcome.Stderr.ShouldContain("◌ logged out (alice)");
        outcome.Files.ContainsKey("vault/DOTENVX_ARMOR_TOKEN").ShouldBeFalse();
    }

    [Fact]
    public async Task LogoutErrorMatchesNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(401,
            """{"error":{"code":"UNAUTHORIZED","message":"bad token","help":"https://armor.example/help"}}""");

        Outcome outcome = await Compare("armor", "logout");

        outcome.ExitCode.ShouldBe(1);
        outcome.Stderr.ShouldContain("[UNAUTHORIZED] bad token");
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("nonjson")]
    [InlineData("error")]
    public async Task CurlMatchesNode(string kind)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => kind switch
        {
            "ok" => new FakeResponse(200, """{"a":1,"b":[1,2,{"c":"d"}],"e":{}}"""),
            "nonjson" => new FakeResponse(200, "plain text", "text/plain"),
            _ => new FakeResponse(403, """{"error":{"code":"FORBIDDEN","message":"nope"}}""")
        };

        Outcome outcome = await Compare("curl", Server.BaseUrl + "/api/account");

        outcome.ExitCode.ShouldBe(kind == "error" ? 1 : 0);
    }

    [Fact]
    public async Task CurlPostsJsonLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(201, """{"created":true}""");

        Outcome outcome = await Compare("curl", Server.BaseUrl + "/api/things", "--data", """{"name":  "x", "n": [1, 2]}""");

        outcome.Requests.Single().ShouldContain("POST /api/things");
        outcome.Requests.Single().ShouldContain("""body={"name":"x","n":[1,2]}""");
    }

    [Fact]
    public async Task CurlRequestMethodAndTokenOptionMatchNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();

        Outcome outcome = await Compare("curl", "-X", "delete", Server.BaseUrl + "/api/things/1", "--token", "tok_explicit");

        outcome.Requests.Single().ShouldContain("DELETE /api/things/1");
        outcome.Requests.Single().ShouldContain("Bearer tok_explicit");
    }

    [Theory]
    [InlineData("http://evil.example/api")]
    [InlineData("not a url")]
    [InlineData("--data=bad")]
    public async Task CurlRefusalsMatchNode(string target)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        string[] args = target.StartsWith("--data")
            ? ["curl", Server.BaseUrl + "/x", "--data", "{not json"]
            : ["curl", target];

        Outcome outcome = await Compare(args);

        outcome.ExitCode.ShouldBe(1);
        outcome.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CurlWithoutATokenOrUrlMatchesNode()
    {
        if (!Available)
        {
            return;
        }

        (await Compare("curl", "http://127.0.0.1:1/x")).ExitCode.ShouldBe(1);

        // no url prints the help (the text itself is each CLI's own help renderer) and exits 0
        Reset();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, Env(), "curl");
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, Env(), "curl");
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        node.ExitCode.ShouldBe(0);
    }
}
