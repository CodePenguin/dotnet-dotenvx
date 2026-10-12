using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - end-to-end comparison of `armor up|down|push|pull|move` (dotenvx/src/cli/actions/armor/*.js,
/// lib/services/armor*.js, lib/api/postArmor*.js) against a fake Armor API: every request body, every message and every
/// file written must match the Node CLI byte for byte. `armor open` launches a browser and is covered by unit tests only.
/// </remarks>
public class ArmorKeysParityTest(NodeParityFixture fixture) : ArmorParityTestBase(fixture)
{
    private const string TeamRequired = """
        {"error":{"code":"TEAM_REQUIRED","message":"team required","meta":{"organizations":[{"provider_slug":"acme"},{"provider_slug":"globex"}]}}}
        """;

    private void Project()
    {
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
    }

    private void ProjectWithoutKeys()
    {
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
    }

    private static string KeyJson(bool changed, string? team = null)
    {
        return $$"""{"changed":{{(changed ? "true" : "false")}},"private_key":"{{FixedKeypairFixture.PrivateKey}}"{{(team is null ? "" : $",\"team\":\"{team}\"")}}}""";
    }

    [Theory]
    [InlineData("up", true)]
    [InlineData("up", false)]
    [InlineData("push", true)]
    [InlineData("push", false)]
    public async Task UpAndPushMatchNode(string command, bool changed)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, KeyJson(changed));

        Outcome outcome = await CompareWith(Project, null, null, "armor", command);

        (changed ? outcome.Stderr : outcome.Stdout).ShouldContain(changed
            ? command == "up" ? "⛨ armored (02B 106)" : "⛨ pushed (02B 106)"
            : "no change (02B 106)");
        outcome.Requests.Single().ShouldContain($"/api/armor/{command}");
        if (command == "up")
        {
            outcome.Files.ContainsKey(".env.keys").ShouldBeFalse("armor up removes the key from .env.keys");
        }
    }

    [Theory]
    [InlineData("down", true)]
    [InlineData("down", false)]
    [InlineData("pull", true)]
    [InlineData("pull", false)]
    public async Task DownAndPullMatchNode(string command, bool changed)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, KeyJson(changed));

        Outcome outcome = await CompareWith(ProjectWithoutKeys, null, null, "armor", command);

        outcome.Files[".env.keys"].ShouldContain($"DOTENV_PRIVATE_KEY={FixedKeypairFixture.PrivateKey}");
    }

    [Fact]
    public async Task DownKeepsOtherKeysInTheKeysFile()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, KeyJson(true));

        await CompareWith(() =>
        {
            ProjectWithoutKeys();
            WriteFile(".env.keys", "OTHER=1\nDOTENV_PRIVATE_KEY=old\n");
        }, null, null, "armor", "down");
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("push")]
    [InlineData("pull")]
    public async Task ExplicitTeamAndTokenMatchNode(string command)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, KeyJson(true));

        Outcome outcome = await CompareWith(command is "up" or "push" ? Project : ProjectWithoutKeys, null, null,
            "armor", command, "--team", "acme", "--token", "tok_explicit", "-f", ".env");

        outcome.Requests.Single().ShouldContain("Bearer tok_explicit");
        outcome.Requests.Single().ShouldContain("\"team\":\"acme\"");
    }

    [Theory]
    [InlineData("\n", "acme")]
    [InlineData("[B\n", "globex")]
    public async Task TeamRequiredPromptsForTheTeamLikeNode(string input, string expectedTeam)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = request => request.NormalizedBody.Contains("\"team\"")
            ? new FakeResponse(200, KeyJson(true))
            : new FakeResponse(400, TeamRequired);

        CompareStderr = false;

        // "[B" is the down arrow escape sequence: ESC [ B
        Outcome outcome = await CompareWith(Project, input.Replace("[B", (char)27 + "[B"), null, "armor", "up");

        outcome.Requests.Length.ShouldBe(2);
        outcome.Requests[1].ShouldContain($"\"team\":\"{expectedTeam}\"");
    }

    [Fact]
    public async Task TeamRequiredWithASingleOrganizationSkipsThePrompt()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = request => request.NormalizedBody.Contains("\"team\"")
            ? new FakeResponse(200, KeyJson(true))
            : new FakeResponse(400,
                """{"error":{"code":"DOTENVX_TEAM_REQUIRED","message":"team required","meta":{"organizations":[{"provider_slug":"solo"}]}}}""");

        CompareStderr = false;
        Outcome outcome = await CompareWith(ProjectWithoutKeys, "", null, "armor", "pull");

        outcome.Requests[1].ShouldContain("\"team\":\"solo\"");
    }

    [Fact]
    public async Task MoveSelectsTheTeamFromTheAccountLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        CompareStderr = false;
        Server.Handler = request => request.Path switch
        {
            "/api/account" => new FakeResponse(200,
                """{"organizations":[{"provider_slug":"acme"},{"provider_slug":"globex"}]}"""),
            _ => new FakeResponse(200, KeyJson(true, "globex"))
        };

        Outcome outcome = await CompareWith(ProjectWithoutKeys, (char)27 + "[B\n", null, "armor", "move");

        outcome.Stderr.ShouldContain("⛨ moved to globex (02B 106)");
        outcome.Requests.Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(401, """{"error":{"code":"UNAUTHORIZED","message":"bad token","help":"https://armor.example/help"}}""")]
    [InlineData(422, """{"error":{"message":"unprocessable"}}""")]
    [InlineData(500, """{"error":{"code":"BOOM","message":"server error","meta":{"x":1}}}""")]
    public async Task ApiErrorsAreReportedLikeNode(int status, string body)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(status, body);

        Outcome outcome = await CompareWith(Project, null, null, "armor", "up");

        outcome.ExitCode.ShouldBe(1);
        outcome.Stderr.ShouldContain("☠");
    }

    [Fact]
    public async Task NonJsonResponseIsReportedLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(502, "<html>Bad Gateway</html>", "text/html");

        Outcome outcome = await CompareWith(Project, null, null, "armor", "up");

        outcome.ExitCode.ShouldBe(1);
    }

    [Theory]
    [InlineData("up", false)]
    [InlineData("push", false)]
    [InlineData("down", true)]
    public async Task MissingFilesAreReportedLikeNode(string command, bool keys)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, KeyJson(true));

        // `up`/`push` read the private key from .env.keys; `down` needs the .env file's public key
        Outcome outcome = await CompareWith(keys ? null : ProjectWithoutKeys, null, null, "armor", command);

        outcome.ExitCode.ShouldBe(1);
    }

    [Fact]
    public async Task UnknownArmorCommandPrintsTheBannerLikeNode()
    {
        if (!Available)
        {
            return;
        }

        Outcome outcome = await Compare("armor", "bogus");

        outcome.Stdout.ShouldContain("Dotenvx Armor");
        outcome.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task ArmorWithoutACommandPrintsHelp()
    {
        if (!Available)
        {
            return;
        }

        Reset();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, Env(), "armor");
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, Env(), "armor");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        foreach (string word in new[] { "up", "down", "push", "pull", "open", "move", "login", "logout", "status", "settings" })
        {
            dotnet.StandardOutput.ShouldContain(word);
        }
    }
}
