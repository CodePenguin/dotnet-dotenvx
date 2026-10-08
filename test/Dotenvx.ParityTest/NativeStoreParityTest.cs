using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - end-to-end comparison of the `native` command group (dotenvx/src/cli/commands/native.js,
/// cli/actions/keychain/*.js, lib/services/keychain*.js) and of the native key provider (lib/providers/native) used by
/// run/get/validate/decrypt/keypair. The OS secret store is faked by test/Dotenvx.FakeSecretCli installed as
/// `powershell.exe` (Windows) or `secret-tool` (Linux) on PATH, which both CLIs reach through the same upstream
/// commands. macOS (`/usr/bin/security` is an absolute path) is covered by MacosKeychainParityTest.
/// </remarks>
public class NativeStoreParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static readonly string FakeTool = OperatingSystem.IsWindows() ? "powershell" : "secret-tool";

    private string VaultDir => Path.Combine(WorkDir, "vault");
    private string BinDir => Path.Combine(WorkDir, "fakebin");
    private string StoreFile => Path.Combine(VaultDir, "native", "dotenvx_" + FixedKeypairFixture.PublicKey);

    private bool Available => EnsureAvailable() && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux());

    private Dictionary<string, string> Env(Dictionary<string, string>? extra = null)
    {
        var env = new Dictionary<string, string>
        {
            ["PATH"] = BinDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["FAKE_SECRET_CLI_DIR"] = VaultDir,
            ["CI"] = ""
        };

        foreach ((string key, string value) in extra ?? [])
        {
            env[key] = value;
        }

        return env;
    }

    private record Snapshot(int ExitCode, string Stdout, string Stderr, string? KeysFile, string? StoreContent);

    private void Reset(bool inStore, bool inKeysFile)
    {
        if (Directory.Exists(VaultDir))
        {
            Directory.Delete(VaultDir, true);
        }

        Directory.CreateDirectory(Path.Combine(VaultDir, "native"));
        FakeCliInstaller.Install(BinDir, FakeTool);
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        File.Delete(Path.Combine(WorkDir, ".env.keys"));
        if (inKeysFile)
        {
            WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
        }

        if (inStore)
        {
            File.WriteAllText(StoreFile, FixedKeypairFixture.PrivateKey);
        }
    }

    private Snapshot Capture(CliResult result)
    {
        string keys = Path.Combine(WorkDir, ".env.keys");

        return new Snapshot(result.ExitCode, result.StandardOutput, result.StandardError,
            File.Exists(keys) ? File.ReadAllText(keys) : null,
            File.Exists(StoreFile) ? File.ReadAllText(StoreFile) : null);
    }

    private async Task<Snapshot> Compare(bool inStore, bool inKeysFile, Dictionary<string, string>? extraEnv,
        params string[] args)
    {
        Dictionary<string, string> env = Env(extraEnv);

        Reset(inStore, inKeysFile);
        Snapshot node = Capture(await Fixture.RunNodeAsync(WorkDir, env, args));

        Reset(inStore, inKeysFile);
        Snapshot dotnet = Capture(await Fixture.RunDotnetAsync(WorkDir, env, args));

        dotnet.ExitCode.ShouldBe(node.ExitCode, $"stderr: {dotnet.Stderr}");
        dotnet.Stdout.ShouldBe(node.Stdout);
        dotnet.Stderr.ShouldBe(node.Stderr);
        dotnet.KeysFile.ShouldBe(node.KeysFile);
        dotnet.StoreContent.ShouldBe(node.StoreContent);

        return node;
    }

    [Theory]
    [InlineData("up", false, true)]
    [InlineData("up", true, true)]
    [InlineData("up", true, false)]
    [InlineData("up", false, false)]
    [InlineData("down", true, false)]
    [InlineData("down", true, true)]
    [InlineData("down", false, true)]
    [InlineData("down", false, false)]
    [InlineData("push", false, true)]
    [InlineData("push", true, true)]
    [InlineData("push", false, false)]
    [InlineData("pull", true, false)]
    [InlineData("pull", true, true)]
    [InlineData("pull", false, true)]
    public async Task NativeSubcommandsMatchNode(string subcommand, bool inStore, bool inKeysFile)
    {
        if (!Available)
        {
            return;
        }

        await Compare(inStore, inKeysFile, null, "native", subcommand);
    }

    [Fact]
    public async Task NativeUpMovesTheKeyIntoTheStore()
    {
        if (!Available)
        {
            return;
        }

        Snapshot snapshot = await Compare(false, true, null, "native", "up");

        snapshot.KeysFile.ShouldBeNull("the keys file is deleted once its last key moved");
        snapshot.StoreContent.ShouldBe(FixedKeypairFixture.PrivateKey);
        snapshot.Stderr.ShouldContain("⌥ stored (02B 106)");
    }

    [Fact]
    public async Task NativeDownMovesTheKeyBackToTheKeysFile()
    {
        if (!Available)
        {
            return;
        }

        Snapshot snapshot = await Compare(true, false, null, "native", "down");

        snapshot.KeysFile.ShouldBe(FixedKeypairFixture.PrivateKeyFile);
        snapshot.StoreContent.ShouldBeNull();
    }

    [Fact]
    public async Task NativeWithoutSubcommandPrintsHelpLikeNode()
    {
        if (!Available)
        {
            return;
        }

        Reset(false, false);
        CliResult node = await Fixture.RunNodeAsync(WorkDir, Env(), "native");
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, Env(), "native");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        foreach (string word in new[] { "up", "down", "push", "pull" })
        {
            dotnet.StandardOutput.ShouldContain(word);
        }
    }

    [Fact]
    public async Task NativeFileOptionsMatchNode()
    {
        if (!Available)
        {
            return;
        }

        Reset(false, false);
        WriteFile("sub/.env", FixedKeypairFixture.EncryptedEnv);
        WriteFile("other.keys", FixedKeypairFixture.PrivateKeyFile);
        string[] args = ["native", "up", "-f", "sub/.env", "-fk", "other.keys"];

        CliResult node = await Fixture.RunNodeAsync(WorkDir, Env(), args);
        string? nodeStore = File.Exists(StoreFile) ? File.ReadAllText(StoreFile) : null;
        bool nodeKeysExists = File.Exists(Path.Combine(WorkDir, "other.keys"));

        Reset(false, false);
        WriteFile("sub/.env", FixedKeypairFixture.EncryptedEnv);
        WriteFile("other.keys", FixedKeypairFixture.PrivateKeyFile);
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, Env(), args);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        (File.Exists(StoreFile) ? File.ReadAllText(StoreFile) : null).ShouldBe(nodeStore);
        File.Exists(Path.Combine(WorkDir, "other.keys")).ShouldBe(nodeKeysExists);
    }

    [Theory]
    [InlineData("get", "HELLO")]
    [InlineData("get")]
    [InlineData("run", "--", "node", "-e", "console.log(process.env.HELLO)")]
    [InlineData("validate")]
    [InlineData("keypair")]
    [InlineData("decrypt", "--stdout")]
    public async Task ProviderFindsTheKeyInTheStoreLikeNode(params string[] args)
    {
        if (!Available)
        {
            return;
        }

        Snapshot snapshot = await Compare(true, false, null, args);

        if (args[0] == "get" && args.Length > 1)
        {
            snapshot.Stdout.ShouldContain("World");
        }
    }

    [Theory]
    [InlineData("--no-native")]
    [InlineData("CI")]
    public async Task ProviderIsOffWithNoNativeOrCi(string how)
    {
        if (!Available)
        {
            return;
        }

        bool flag = how.StartsWith("--");
        Snapshot snapshot = await Compare(true, false,
            flag ? null : new Dictionary<string, string> { ["CI"] = "true" },
            flag ? ["get", "HELLO", how] : ["get", "HELLO"]);

        snapshot.Stdout.ShouldNotContain("World");
    }

    [Theory]
    [InlineData("run", "--no-native", "--", "node", "-e", "console.log(process.env.HELLO)")]
    [InlineData("validate", "--no-native")]
    [InlineData("keypair", "--no-native")]
    [InlineData("decrypt", "--stdout", "--no-native")]
    [InlineData("set", "B", "2", "--no-native")]
    [InlineData("encrypt", "--no-native")]
    public async Task NoNativeIsAcceptedEverywhereLikeNode(params string[] args)
    {
        if (!Available)
        {
            return;
        }

        // key in the file as well as the store: the flag must not change anything observable except the store lookup
        Dictionary<string, string> env = Env();
        Reset(true, true);
        CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);
        string nodeEnv = ReadFile(".env");

        Reset(true, true);
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);

        dotnet.ExitCode.ShouldBe(node.ExitCode, $"stderr: {dotnet.StandardError}");
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        RedactKeyMaterial(ReadFile(".env")).ShouldBe(RedactKeyMaterial(nodeEnv));
    }
}
