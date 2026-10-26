using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - end-to-end comparison of the Armor key provider and server-side decryptor
/// (dotenvx/src/lib/providers/armor, decryptors/armor, helpers/parseWithDecryptor.js) as used by get/run/validate/decrypt/
/// keypair, and of key creation with Armor on in set/encrypt (storage selection prompt + `armor up`), against a fake
/// Armor API. The OS secret store is the fake shim, so the native provider is consulted first and finds nothing.
/// </remarks>
public class ArmorProviderParityTest(NodeParityFixture fixture) : ArmorParityTestBase(fixture)
{
    private string Keyring => $$"""{"{{FixedKeypairFixture.PublicKey}}":"{{FixedKeypairFixture.PrivateKey}}"}""";

    private static string ServerSideRequired()
    {
        return """{"error":{"code":"SERVER_SIDE_DECRYPTION_REQUIRED","message":"decrypt on server","meta":{"public_key":"PUBKEY","grant_token":"grantX"}}}"""
            .Replace("PUBKEY", FixedKeypairFixture.PublicKey);
    }

    private void EncryptedProject()
    {
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
    }

    [Theory]
    [InlineData("get", "HELLO")]
    [InlineData("get")]
    [InlineData("keypair")]
    [InlineData("decrypt", "--stdout")]
    [InlineData("run", "--", "node", "-e", "console.log(process.env.HELLO)")]
    public async Task KeyringProviderDecryptsLikeNode(params string[] args)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, Keyring);

        Outcome outcome = await CompareWith(EncryptedProject, null, null, args);

        outcome.Requests.ShouldContain(request => request.Contains("POST /api/armor/keyring"));
        if (args is ["get", "HELLO"])
        {
            outcome.Stdout.ShouldContain("World");
        }
    }

    [Fact]
    public async Task KeyringRequestBodyMatchesNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, Keyring);

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        outcome.Requests.Single(request => request.Contains("/api/armor/keyring")).ShouldContain(
            $"\"public_key\":\"{FixedKeypairFixture.PublicKey}\"");
    }

    [Fact]
    public async Task EmptyKeyringLeavesTheValueEncryptedLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, "{}");

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        outcome.Stdout.ShouldNotContain("World");
    }

    [Fact]
    public async Task ProviderErrorsFallBackToAKeylessParseLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(403,
            """{"error":{"code":"FORBIDDEN","message":"no access","help":"https://armor.example/help"}}""");

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        outcome.Stderr.ShouldContain("[FORBIDDEN] no access");
    }

    [Fact]
    public async Task NetworkErrorsAreSwallowedByTheProviderLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Dispose();

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        // providers 0.3.5 reports the failed connection before the undecryptable value; both CLIs print the same
        outcome.Stderr.ShouldContain("DECRYPTION_FAILED");
    }

    [Theory]
    [InlineData("immediate")]
    [InlineData("pending-first")]
    public async Task AccessApprovalIsPolledLikeNode(string mode)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = request =>
        {
            if (!request.NormalizedBody.Contains("grant_token"))
            {
                return new FakeResponse(403,
                    """{"error":{"code":"ACCESS_APPROVAL_REQUIRED","message":"approval needed","meta":{"grant_token":"grant1","approval_uri":"https://armor.example/approve"}}}""");
            }

            // the request being answered is already in the log: the first grant request is "pending" in this mode
            return mode == "pending-first" && Server.RequestLog().Count(r => r.Contains("grant_token")) == 1
                ? new FakeResponse(403, """{"error":{"code":"ACCESS_PENDING","message":"still pending"}}""")
                : new FakeResponse(200, Keyring);
        };

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        outcome.Stdout.ShouldContain("World");
        outcome.Requests.Count(r => r.Contains("grant1")).ShouldBe(mode == "immediate" ? 1 : 2);
    }

    [Fact]
    public async Task ServerSideDecryptionUsesTheDecryptorLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = request => request.Path switch
        {
            "/api/armor/keyring" => new FakeResponse(409,
                ServerSideRequired()),
            "/api/armor/decrypt" => new FakeResponse(200, """{"src":"HELLO=\"from armor\"\n"}"""),
            _ => new FakeResponse(404, "{}")
        };

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        outcome.Stdout.ShouldContain("from armor");
        outcome.Requests.ShouldContain(request => request.Contains("POST /api/armor/decrypt"));
    }

    [Fact]
    public async Task DecryptorFailureFallsBackLikeNode()
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = request => request.Path switch
        {
            "/api/armor/keyring" => new FakeResponse(409,
                ServerSideRequired()),
            _ => new FakeResponse(500, """{"error":{"code":"BOOM","message":"decryptor down"}}""")
        };

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "get", "HELLO");

        outcome.Stderr.ShouldContain("[BOOM] decryptor down");
    }

    [Theory]
    [InlineData("--no-armor")]
    [InlineData("DOTENVX_NO_ARMOR")]
    public async Task ArmorCanBeDisabledLikeNode(string how)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        Server.Handler = _ => new FakeResponse(200, Keyring);

        Outcome outcome = how.StartsWith("--")
            ? await CompareWith(EncryptedProject, null, null, "get", "HELLO", how)
            : await CompareWith(EncryptedProject, null, new Dictionary<string, string> { [how] = "true" }, "get", "HELLO");

        outcome.Requests.ShouldBeEmpty();
        outcome.Stdout.ShouldNotContain("World");
    }

    [Fact]
    public async Task LoggedOutOrTurnedOffNeverCallsTheApi()
    {
        if (!Available)
        {
            return;
        }

        Server.Handler = _ => new FakeResponse(200, Keyring);
        (await CompareWith(EncryptedProject, null, null, "get", "HELLO")).Requests.ShouldBeEmpty();

        LoggedIn(on: false);
        (await CompareWith(EncryptedProject, null, null, "get", "HELLO")).Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TokenOptionForcesArmorOnLikeNode()
    {
        if (!Available)
        {
            return;
        }

        // session turned off, but --token skips the session check (run accepts it)
        LoggedIn(on: false);
        Server.Handler = _ => new FakeResponse(200, Keyring);

        Outcome outcome = await CompareWith(EncryptedProject, null, null, "run", "--token", "tok_x", "--", "node", "-e", "1");

        outcome.Requests.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("set", "A", "1")]
    [InlineData("encrypt")]
    public async Task FileStorageChoiceKeepsTheKeyInEnvKeysLikeNode(params string[] args)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        CompareStderr = false;
        RedactFiles = true;
        RedactRequests = true;

        Outcome outcome = await CompareWith(args[0] == "set" ? null : () => WriteFile(".env", "A=1\n"), "\n",
            new Dictionary<string, string> { ["DOTENVX_NO_NATIVE"] = "true" }, args);

        outcome.Files.ShouldContainKey(".env.keys");
        outcome.Requests.ShouldAllBe(request => request.Contains("/api/events"), "file storage sends no key to Armor (only the events report)");
    }

    [Theory]
    [InlineData("set", "A", "1", "--no-armor")]
    [InlineData("encrypt", "--no-armor")]
    public async Task NoArmorSkipsTheStoragePromptLikeNode(params string[] args)
    {
        if (!Available)
        {
            return;
        }

        LoggedIn();
        CompareStderr = false;
        RedactFiles = true;

        Outcome outcome = await CompareWith(args[0] == "set" ? null : () => WriteFile(".env", "A=1\n"), "", new Dictionary<string, string> { ["DOTENVX_NO_NATIVE"] = "true" }, args);

        outcome.Files.ShouldContainKey(".env.keys");
        outcome.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task NoPromptWhenLoggedOutLikeNode()
    {
        if (!Available)
        {
            return;
        }

        // since 2.38.0 the closing line names the new public key (random per run), so stderr cannot be compared byte for byte
        CompareStderr = false;
        RedactFiles = true;

        Outcome outcome = await CompareWith(null, "", new Dictionary<string, string> { ["DOTENVX_NO_NATIVE"] = "true" }, "set", "A", "1");

        outcome.Files.ShouldContainKey(".env.keys");
    }
}
