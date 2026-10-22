using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): key custody in 1Password and Bitwarden
/// (src/lib/custodians/local/onepassword.js, bitwarden.js, services/custodyTransfer.js, helpers/resolveLocalKey.js). A scenario is a
/// sequence of CLI steps (`dotenvx 1password up`, `get`, `down`, ...) run from the same on-disk state by the real Node CLI and by the .NET
/// CLI against the fake `op`/`bw` of test/fakes/secretcli.cs; every step's exit code and output, the key file, the dotenvx settings store,
/// the fake vault and the list of calls made to the fake CLIs must match.
/// </remarks>
public class CustodyParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private sealed record Step(string[] Args, string[]? Set = null, string[]? Clear = null, bool DeleteKeysFile = false,
        Dictionary<string, string>? Env = null);

    private static Step S(params string[] args) => new(args);

    private static Step With(Step step, params string[] flags) => step with { Set = flags };

    private static Step Without(Step step, params string[] flags) => step with { Clear = flags };

    private static Step NoKeys(Step step) => step with { DeleteKeysFile = true };

    private static Step Env(Step step, string name, string value) =>
        step with { Env = new Dictionary<string, string>(step.Env ?? []) { [name] = value } };

    private static readonly string[] OpUp = ["1password", "up", "-f", ".env"];
    private static readonly string[] OpDown = ["1password", "down", "-f", ".env"];
    private static readonly string[] OpPush = ["1password", "push", "-f", ".env"];
    private static readonly string[] OpPull = ["1password", "pull", "-f", ".env"];
    private static readonly string[] BwUp = ["bitwarden", "up", "-f", ".env"];
    private static readonly string[] BwDown = ["bitwarden", "down", "-f", ".env"];
    private static readonly string[] BwPush = ["bitwarden", "push", "-f", ".env"];
    private static readonly string[] BwPull = ["bitwarden", "pull", "-f", ".env"];
    private static readonly string[] Get = ["get", "HELLO", "-f", ".env"];

    public static IEnumerable<object[]> Cases() => Scenarios().Select(scenario => new object[] { scenario.Name });

    private static IEnumerable<(string Name, Step[] Steps)> Scenarios()
    {
        static (string, Step[]) Scenario(string name, params Step[] steps) => (name, steps);

        // 1Password
        yield return Scenario("op up get down get", S(OpUp), S(Get), S(OpDown), S(Get));
        yield return Scenario("op push pull get", S(OpPush), NoKeys(S(OpPull)), S(Get));
        yield return Scenario("op up twice", S(OpUp), S(OpUp));
        yield return Scenario("op push twice", S(OpPush), S(OpPush));
        yield return Scenario("op down without up", S(OpDown));
        yield return Scenario("op pull without up", S(OpPull));
        yield return Scenario("op up no keys file", NoKeys(S(OpUp)));
        yield return Scenario("op signed out signs in", With(S(OpUp), "op-signedout"), S(Get));
        yield return Scenario("op signed out signin fails", With(S(OpUp), "op-signedout", "op-signin-fails"));
        yield return Scenario("op signed out service account", Env(With(S(OpUp), "op-signedout"), "OP_SERVICE_ACCOUNT_TOKEN", "token"));
        yield return Scenario("op bad account id", With(S(OpUp), "op-account:bad"));
        yield return Scenario("op bad item id", With(S(OpUp), "op-create-badid"));
        yield return Scenario("op bad vault id", With(S(OpUp), "op-create-novault"));
        yield return Scenario("op verify mismatch", With(S(OpUp), "op-read-wrong"));
        yield return Scenario("op missing cli", With(S(OpUp), "op-missing"));
        yield return Scenario("op get after delete", S(OpUp), S(OpDown), NoKeys(S(Get)));
        yield return Scenario("op get with cli gone", S(OpUp), With(S(Get), "op-missing"));
        yield return Scenario("op disabled by option", S(OpUp), S("get", "HELLO", "-f", ".env", "--no-1password"));
        yield return Scenario("op disabled by env", S(OpUp), Env(S(Get), "DOTENVX_NO_1PASSWORD", "true"));
        yield return Scenario("op run after up", S(OpUp), S("run", "-f", ".env", "--", "node", "-e", "console.log(process.env.HELLO)"));

        // Bitwarden
        yield return Scenario("bw up get down get", S(BwUp), S(Get), S(BwDown), S(Get));
        yield return Scenario("bw push pull get", S(BwPush), NoKeys(S(BwPull)), S(Get));
        yield return Scenario("bw up twice", S(BwUp), S(BwUp));
        yield return Scenario("bw down without up", S(BwDown));
        yield return Scenario("bw pull without up", S(BwPull));
        yield return Scenario("bw up no keys file", NoKeys(S(BwUp)));
        yield return Scenario("bw unauthenticated", With(S(BwUp), "bw-status:unauthenticated"));
        yield return Scenario("bw locked non interactive", With(S(BwUp), "bw-status:locked"));
        yield return Scenario("bw garbage status", With(S(BwUp), "bw-status:garbage"));
        yield return Scenario("bw personal vault only", With(S(BwUp), "bw-create-org"));
        yield return Scenario("bw create garbage", With(S(BwUp), "bw-create-garbage"));
        yield return Scenario("bw verify mismatch", With(S(BwUp), "bw-get-wrong"));
        yield return Scenario("bw missing cli", With(S(BwUp), "bw-missing"));
        yield return Scenario("bw account mismatch", S(BwUp), With(S(Get), "bw-user:33333333-3333-3333-3333-333333333333"));
        yield return Scenario("bw account mismatch down", S(BwUp), With(S(BwDown), "bw-user:33333333-3333-3333-3333-333333333333"));
        yield return Scenario("bw server mismatch", S(BwUp), With(S(Get), "bw-server:https://example.com"));
        yield return Scenario("bw get with cli gone", S(BwUp), With(S(Get), "bw-missing"));
        yield return Scenario("bw disabled by option", S(BwUp), S("get", "HELLO", "-f", ".env", "--no-bitwarden"));
        yield return Scenario("bw disabled by env", S(BwUp), Env(S(Get), "DOTENVX_NO_BITWARDEN", "true"));
        yield return Scenario("bw up when already stored", S(BwPush), S(BwUp));

        // both
        yield return Scenario("op and bw both", S(OpPush), S(BwPush), S(Get), S(OpDown), S(Get));
    }

    private static string Normalise(string text, string workDir, string home)
    {
        text = text.Replace(workDir.Replace('\\', '/'), "<dir>").Replace(workDir, "<dir>");
        text = text.Replace(home.Replace('\\', '/'), "<cfg>").Replace(home, "<cfg>");

        // the update check (network and clock) writes DOTENVX_VERSION and DOTENVX_VERSION_LAST_CHECK into the settings
        return System.Text.RegularExpressions.Regex.Replace(text.Replace("\r\n", "\n"), "DOTENVX_VERSION(_LAST_CHECK)?=\"[^\"]*\"\n?", "");
    }

    private static void Reset(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(directory);
    }

    private async Task<List<string>> Run(bool node, Step[] steps)
    {
        string vault = Path.Combine(WorkDir, "vault");
        string bin = Path.Combine(WorkDir, "fakebin");
        string home = Path.Combine(WorkDir, "home");
        string project = Path.Combine(WorkDir, "project");
        Reset(project);
        Reset(vault);
        Reset(home);
        Directory.CreateDirectory(Path.Combine(vault, "op"));
        Directory.CreateDirectory(Path.Combine(vault, "bw"));
        File.WriteAllText(Path.Combine(vault, "session"), "unlocked-session");
        File.WriteAllText(Path.Combine(vault, "master"), "master-pw");
        FakeCliInstaller.Install(bin, "op", "bw");
        File.WriteAllText(Path.Combine(project, ".env"), FixedKeypairFixture.EncryptedEnv);
        File.WriteAllText(Path.Combine(project, ".env.keys"), FixedKeypairFixture.PrivateKeyFile);

        var log = new List<string>();
        foreach (Step step in steps)
        {
            foreach (string flag in step.Set ?? [])
            {
                string[] parts = flag.Split(':', 2);
                File.WriteAllText(Path.Combine(vault, parts[0]), parts.Length > 1 ? parts[1] : "");
            }

            foreach (string flag in step.Clear ?? [])
            {
                File.Delete(Path.Combine(vault, flag));
            }

            if (step.DeleteKeysFile)
            {
                File.Delete(Path.Combine(project, ".env.keys"));
            }

            var env = new Dictionary<string, string>
            {
                ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                ["FAKE_SECRET_CLI_DIR"] = vault,
                ["BW_SESSION"] = "session-token",
                ["DOTENVX_CONFIG"] = Path.Combine(home, "dotenvx"),
                ["DOTENVX_NO_NATIVE"] = "true"
            };
            foreach ((string key, string value) in step.Env ?? [])
            {
                env[key] = value;
            }

            CliResult result = node
                ? await Fixture.RunNodeAsync(project, env, step.Args)
                : await Fixture.RunDotnetAsync(project, env, step.Args);
            log.Add($"$ {string.Join(' ', step.Args)}\nexit {result.ExitCode}\nout: {Normalise(result.StandardOutput, project, home)}\n" +
                    $"err: {Normalise(result.StandardError, project, home)}");
        }

        log.Add("keys file:\n" + (File.Exists(Path.Combine(project, ".env.keys")) ? File.ReadAllText(Path.Combine(project, ".env.keys")) : "<none>"));
        foreach (string file in Directory.EnumerateFiles(home, "*", SearchOption.AllDirectories).Order())
        {
            log.Add($"settings {Path.GetRelativePath(home, file).Replace('\\', '/')}:\n" + Normalise(File.ReadAllText(file), project, home).Trim());
        }

        foreach (string file in Directory.EnumerateFiles(vault, "*", SearchOption.AllDirectories).Order())
        {
            log.Add($"vault {Path.GetRelativePath(vault, file).Replace('\\', '/')}:\n" + Normalise(File.ReadAllText(file), project, home).Trim());
        }

        return log;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesNode(string name)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        Step[] typed = Scenarios().First(scenario => scenario.Name == name).Steps;
        List<string> node = await Run(true, typed);
        List<string> dotnet = await Run(false, typed);

        if (Environment.GetEnvironmentVariable("CUSTODY_DUMP") is { Length: > 0 } dump)
        {
            string file = System.Text.RegularExpressions.Regex.Replace(name, "[^A-Za-z0-9]+", "_");
            File.WriteAllText(Path.Combine(dump, file + ".node.txt"), string.Join("\n---\n", node));
            File.WriteAllText(Path.Combine(dump, file + ".port.txt"), string.Join("\n---\n", dotnet));
        }

        string.Join("\n---\n", dotnet).ShouldBe(string.Join("\n---\n", node), name);
    }
}
