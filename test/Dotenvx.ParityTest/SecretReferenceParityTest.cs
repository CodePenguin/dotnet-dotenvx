using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - end-to-end comparison of `op://` (1Password) and `bw://` (Bitwarden) secret reference
/// resolution (dotenvx/src/lib/helpers/resolveOnePassword.js, resolveBitwardenPassword.js) in `get`, `run` and
/// `validate`. `op`/`bw` are replaced by test/fakes/secretcli.cs on PATH, so Node and .NET talk to the same fake vault
/// and every invocation is recorded in vault/calls.log.
/// </remarks>
public class SecretReferenceParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private string VaultDir => Path.Combine(WorkDir, "vault");
    private string BinDir => Path.Combine(WorkDir, "fakebin");

    private Dictionary<string, string> Env(bool installOp = true, bool installBw = true,
        string? bwSession = "session-token", Dictionary<string, string>? extra = null)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var env = new Dictionary<string, string>
        {
            ["PATH"] = BinDir + Path.PathSeparator + path,
            ["FAKE_SECRET_CLI_DIR"] = VaultDir,
            // the harness never has a real vault session
            ["BW_SESSION"] = bwSession ?? ""
        };

        if (!installOp && !installBw)
        {
            env["PATH"] = path;
        }

        foreach ((string key, string value) in extra ?? [])
        {
            env[key] = value;
        }

        return env;
    }

    private void Vault(bool installOp = true, bool installBw = true)
    {
        Directory.CreateDirectory(Path.Combine(VaultDir, "op"));
        Directory.CreateDirectory(Path.Combine(VaultDir, "bw"));
        File.WriteAllText(Path.Combine(VaultDir, "op", "op___vault_item_field"), "op-secret");
        File.WriteAllText(Path.Combine(VaultDir, "op", "op___vault_item_multi"), "line1\nline2\n");
        File.WriteAllText(Path.Combine(VaultDir, "bw", "github_password"), "bw-secret");
        File.WriteAllText(Path.Combine(VaultDir, "bw", "folder_item_username"), "bw-user");
        File.WriteAllText(Path.Combine(VaultDir, "session"), "unlocked-session");
        File.WriteAllText(Path.Combine(VaultDir, "master"), "master-pw");
        File.Delete(Path.Combine(VaultDir, "calls.log"));

        FakeCliInstaller.Install(BinDir, [.. installOp ? new[] { "op" } : [], .. installBw ? new[] { "bw" } : []]);
    }

    private string[] Calls()
    {
        string log = Path.Combine(VaultDir, "calls.log");

        return File.Exists(log) ? File.ReadAllLines(log) : [];
    }

    private async Task AssertSameAsNode(string envContent, Dictionary<string, string> env, params string[] args)
    {
        // an env without the fake bin directory first simulates "not installed"
        bool tools = env["PATH"].StartsWith(BinDir);

        void Setup()
        {
            WriteFile(".env", envContent);
            if (tools)
            {
                Vault();
            }
            else
            {
                Directory.CreateDirectory(VaultDir);
                File.Delete(Path.Combine(VaultDir, "calls.log"));
            }
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);
        string[] nodeCalls = Calls();

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);
        string[] dotnetCalls = Calls();

        dotnet.ExitCode.ShouldBe(node.ExitCode, $"stderr: {dotnet.StandardError}");
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnetCalls.ShouldBe(nodeCalls);
        LastCalls = nodeCalls;
    }

    private string[] LastCalls { get; set; } = [];

    [Fact]
    public async Task OnePasswordReferenceResolvesInGet()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/item/field\nB=plain\n", Env(), "get");
        // sanity: the fake vault really was consulted
        LastCalls.ShouldContain("op read op://vault/item/field --no-newline");
    }

    [Fact]
    public async Task OnePasswordPreservesTrailingNewlinesFromNoNewline()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/item/multi\n", Env(), "get", "A");
    }

    [Fact]
    public async Task OnePasswordFailureReportsErrorLikeNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/missing/field\nB=ok\n", Env(), "get");
    }

    [Fact]
    public async Task OnePasswordNotInstalledReportsErrorLikeNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/item/field\n", Env(installOp: false, installBw: false), "get");
    }

    [Theory]
    [InlineData("--no-1password")]
    [InlineData("--no-bitwarden")]
    [InlineData("DOTENVX_NO_1PASSWORD")]
    [InlineData("DOTENVX_NO_BITWARDEN")]
    public async Task DisablingAResolverLeavesItsReferencesUntouched(string how)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        bool flag = how.StartsWith("--");
        Dictionary<string, string> env = Env(extra: flag ? null : new Dictionary<string, string> { [how] = "true" });
        string[] args = flag ? ["get", how] : ["get"];

        await AssertSameAsNode("A=op://vault/item/field\nB=bw://github/password\n", env, args);
    }

    [Fact]
    public async Task OnePasswordReferenceResolvesInRun()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/item/field\n", Env(), "run", "--", "node", "-e",
            "console.log(process.env.A)");
    }

    [Fact]
    public async Task OnePasswordReferenceFailsValidateLikeNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/missing/field\n", Env(), "validate");
    }

    [Fact]
    public async Task BitwardenReferencesResolveInGet()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=bw://github/password\nB=bw://folder/item/username\n", Env(), "get");
        LastCalls.ShouldContain("bw get password github [BW_SESSION=session-token]");
    }

    [Theory]
    [InlineData("bw://github")]
    [InlineData("bw://github/")]
    [InlineData("bw://github/notes")]
    [InlineData("bw://missing/password")]
    public async Task BitwardenBadReferencesReportLikeNode(string reference)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode($"A={reference}\n", Env(), "get");
    }

    [Fact]
    public async Task BitwardenLockedVaultReportsLikeNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=bw://github/password\n", Env(bwSession: null), "get");
    }

    [Fact]
    public async Task BitwardenNotInstalledReportsLikeNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=bw://github/password\n", Env(installOp: false, installBw: false), "get");
    }

    [Fact]
    public async Task MixedReferencesAndPlainValuesMatchNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await AssertSameAsNode("A=op://vault/item/field\nB=bw://github/password\nC=\"op://vault/missing/x\"\nD=1\n",
            Env(), "get", "--format", "shell");
    }
}
