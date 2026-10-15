using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): the option and error branches of `run`, `set`, `get`,
/// `keypair`, `ls`, `del`, `encrypt`, `decrypt`, `check` and `primitives check` that the other parity tests never reach. Each
/// scenario runs the real Node CLI and the .NET CLI from the same on-disk state and compares exit code, stdout, stderr and
/// the resulting file tree.
/// </remarks>
public class CoreCommandsParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string Plain = "HELLO=World\nFOO=bar\n";

    private static readonly string Encrypted = FixedKeypairFixture.EncryptedEnv;

    private static readonly string Keys = "DOTENV_PRIVATE_KEY=" + FixedKeypairFixture.PrivateKey + "\n";

    private static readonly string[] Node1 = ["--", "node", "-e", "console.log(process.env.HELLO + '|' + process.env.FOO)"];

    private string Snapshot()
    {
        var lines = new List<string>();
        foreach (string file in Directory.EnumerateFiles(WorkDir, "*", SearchOption.AllDirectories).Order())
        {
            string relative = Path.GetRelativePath(WorkDir, file).Replace('\\', '/');
            string content = File.ReadAllText(file);
            // generated key material differs on every run
            content = System.Text.RegularExpressions.Regex.Replace(content, "(PUBLIC_KEY[A-Za-z0-9_.]*=\")[0-9a-f]+\"", "$1<public>\"");
            content = System.Text.RegularExpressions.Regex.Replace(content, "(PRIVATE_KEY[A-Za-z0-9_.]*=)[0-9a-f]+", "$1<private>");
            content = System.Text.RegularExpressions.Regex.Replace(content, "encrypted:[A-Za-z0-9+/=]+", "encrypted:<cipher>");
            lines.Add($"--- {relative}\n{content}");
        }

        return string.Join("\n", lines);
    }

    private static string Normalise(string text)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\.\d{6,}'", "'");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[0-9a-f]{64,66}", "<hex>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"encrypted:[A-Za-z0-9+/=]+", "encrypted:<cipher>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\([0-9A-F]{3} [0-9A-F]{3}\)", "(<fingerprint>)");

        return System.Text.RegularExpressions.Regex.Replace(text, @"Could not find file ('[^']+')\.",
            "ENOENT: no such file or directory, open $1");
    }

    private async Task Compare(Dictionary<string, string> files, Dictionary<string, string>? extraEnv, params string[] args)
    {
        await Compare(files, extraEnv, errorOnly: false, args);
    }

    /// <summary>
    /// <paramref name="errorOnly" />: the failure text itself is runtime specific (OS error wording, Node stack traces), so only
    /// require that both CLIs fail the same way: same exit code, an error line on stderr, and identical output before it.
    /// </summary>
    private async Task Compare(Dictionary<string, string> files, Dictionary<string, string>? extraEnv, bool errorOnly, params string[] args)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string home = Path.Combine(Path.GetTempPath(), "dotenvx-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var env = new Dictionary<string, string>
            {
                ["DOTENVX_CONFIG"] = Path.Combine(home, "dotenvx"),
                ["DOTENVX_NO_NATIVE"] = "true"
            };
            foreach ((string key, string value) in extraEnv ?? [])
            {
                env[key] = value;
            }

            void Setup()
            {
                foreach (string entry in Directory.GetFileSystemEntries(WorkDir))
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }

                foreach ((string name, string content) in files)
                {
                    WriteFile(name, content);
                }
            }

            Setup();
            CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);
            string nodeTree = Snapshot();

            Setup();
            CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);
            string dotnetTree = Snapshot();

            string label = $"{string.Join(' ', args)}\nnode out: {node.StandardOutput}\nnode err: {node.StandardError}\ndotnet out: {dotnet.StandardOutput}\ndotnet err: {dotnet.StandardError}";
            dotnet.ExitCode.ShouldBe(node.ExitCode, label);
            if (errorOnly)
            {
                node.ExitCode.ShouldNotBe(0, label);
                string[] nodeLines = Normalise(node.StandardError).Split("\n");
                string[] dotnetLines = Normalise(dotnet.StandardError).Split("\n");
                int nodeError = Array.FindIndex(nodeLines, line => line.StartsWith("☠", StringComparison.Ordinal));
                int dotnetError = Array.FindIndex(dotnetLines, line => line.StartsWith("☠", StringComparison.Ordinal));
                nodeError.ShouldBeGreaterThanOrEqualTo(0, label);
                dotnetError.ShouldBeGreaterThanOrEqualTo(0, label);
                dotnetLines[..dotnetError].ShouldBe(nodeLines[..nodeError], label);
                Normalise(dotnet.StandardOutput).ShouldBe(Normalise(node.StandardOutput), label);

                return;
            }

            Normalise(dotnet.StandardOutput).ShouldBe(Normalise(node.StandardOutput), label);
            Normalise(dotnet.StandardError).ShouldBe(Normalise(node.StandardError), label);
            dotnetTree.ShouldBe(nodeTree, label);
        }
        finally
        {
            Directory.Delete(home, true);
        }
    }

    private static Dictionary<string, string> Files(params string[] pairs)
    {
        var files = new Dictionary<string, string>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            files[pairs[i]] = pairs[i + 1];
        }

        return files;
    }

    public static IEnumerable<object[]> Cases()
    {
        Dictionary<string, string> plain = Files(".env", Plain);
        Dictionary<string, string> encrypted = Files(".env", Encrypted, ".env.keys", Keys);
        Dictionary<string, string> none = Files();
        Dictionary<string, string> two = Files(".env", Plain, ".env.local", "FOO=local\nEXTRA=1\n");
        Dictionary<string, string> bad = Files(".env", "HELLO=World\nthis is not valid\n=nokey\n");
        Dictionary<string, string> preset = new() { ["HELLO"] = "preset" };
        Dictionary<string, string> dollar = Files(".env", "A=1\nB=${A}2\nC=$(echo hi)\nD=\"multi\nline\"\n");

        object[] Case(Dictionary<string, string> files, Dictionary<string, string>? env, params string[] args) => [files, env!, args];

        // run: command inference, separators and every logging/ignore/strict branch
        yield return Case(plain, null, "run");
        yield return Case(plain, null, "run", "--");
        yield return Case(plain, null, "run", "-f", ".env");
        yield return Case(plain, null, "run", "-f", ".env", "echo", "hi");
        yield return Case(plain, null, "run", "--quiet");
        yield return Case(plain, null, [.. new[] { "run" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "--mask" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "--mask", "3" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "--mask", "-v" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "-e", "SECRET=topsecretvalue", "--mask", "-v" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "-v" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "--debug" }, .. Node1]);
        yield return Case(plain, preset, [.. new[] { "run", "--debug" }, .. Node1]);
        yield return Case(plain, preset, [.. new[] { "run", "-v" }, .. Node1]);
        yield return Case(plain, preset, [.. new[] { "run", "--overload" }, .. Node1]);
        yield return Case(plain, null, [.. new[] { "run", "--redact", "-v" }, .. Node1]);
        yield return Case(encrypted, null, [.. new[] { "run", "--redact", "--debug" }, .. Node1]);
        yield return Case(encrypted, null, [.. new[] { "run", "-v" }, .. Node1]);
        yield return Case(none, null, [.. new[] { "run", "-f", ".nope" }, .. Node1]);
        yield return Case(none, null, [.. new[] { "run", "-f", ".nope", "-v" }, .. Node1]);
        yield return Case(none, null, [.. new[] { "run", "-f", ".nope", "--strict" }, .. Node1]);
        yield return Case(none, null, [.. new[] { "run", "-f", ".nope", "--ignore", "MISSING_ENV_FILE", "-v" }, .. Node1]);
        yield return Case(none, null, [.. new[] { "run", "-fk", ".keys.none" }, .. Node1]);
        yield return Case(none, null, [.. new[] { "run", "--convention", "nextjs", "-v" }, .. Node1]);
        yield return Case(two, null, [.. new[] { "run", "-f", ".env", "-f", ".env.local", "-v" }, .. Node1]);
        yield return Case(two, null, [.. new[] { "run", "-f", ".env", "-f", ".env.local", "--overload", "--debug" }, .. Node1]);
        yield return Case(two, null, [.. new[] { "run", "--env", "FOO=inline", "-v" }, .. Node1]);
        yield return Case(bad, null, [.. new[] { "run" }, .. Node1]);
        yield return Case(bad, null, [.. new[] { "run", "--strict" }, .. Node1]);
        yield return Case(bad, null, [.. new[] { "run", "-v", "--debug" }, .. Node1]);
        yield return Case(dollar, null, "run", "--", "node", "-e", "console.log(process.env.B + process.env.C + process.env.D)");
        yield return Case(plain, null, "run", "--", "node", "-e", "process.exit(7)");
        yield return Case(plain, null, "run", "--", "no-such-command-anywhere");

        // set
        yield return Case(none, null, "set", "A");
        yield return Case(none, null, "set", "A", "1");
        yield return Case(none, null, "set", "A", "1", "-v");
        yield return Case(none, null, "set", "A", "1", "--plain", "--debug");
        yield return Case(plain, null, "set", "HELLO", "World", "--plain");
        yield return Case(plain, null, "set", "HELLO", "World", "--plain", "-v");
        yield return Case(encrypted, null, "set", "HELLO", "again");
        yield return Case(none, null, "set", "A", "1", "--no-create");
        yield return Case(none, null, "set", "A", "1", "-f", ".env.prod", "-f", ".env.stage");
        yield return Case(none, null, "set", "A", "1", "-fk", "keys/.env.keys");
        yield return Case(plain, null, "set", "A", "1", "-f", ".env", "--no-armor", "--no-native");
        yield return Case(none, null, "set", "A", "1", "--no-bitwarden", "--no-1password");

        // get / keypair / ls / del
        yield return Case(encrypted, null, "get");
        yield return Case(encrypted, null, "get", "HELLO");
        yield return Case(encrypted, null, "get", "NOPE");
        yield return Case(encrypted, null, "get", "HELLO", "--strict");
        yield return Case(encrypted, null, "get", "NOPE", "--strict");
        yield return Case(encrypted, null, "get", "--format", "shell");
        yield return Case(encrypted, null, "get", "--format", "eval", "--all");
        yield return Case(encrypted, null, "get", "--format", "bad");
        yield return Case(encrypted, null, "get", "--pretty-print");
        yield return Case(encrypted, null, "get", "--no-ops-off");
        yield return Case(plain, null, "get", "--all", "-v");
        yield return Case(plain, preset, "get", "HELLO", "--overload");
        yield return Case(none, null, "get", "-f", ".nope");
        yield return Case(encrypted, null, "keypair");
        yield return Case(encrypted, null, "keypair", "DOTENV_PRIVATE_KEY");
        yield return Case(encrypted, null, "keypair", "NOPE");
        yield return Case(encrypted, null, "keypair", "--format", "shell");
        yield return Case(encrypted, null, "keypair", "--pretty-print");
        yield return Case(none, null, "keypair");
        yield return Case(none, null, "keypair", "-f", ".nope");
        yield return Case(two, null, "ls");
        yield return Case(two, null, "ls", "--format", "json");
        yield return Case(two, null, "ls", "--format", "bad");
        yield return Case(two, null, "ls", "-f", ".env.local");
        yield return Case(two, null, "ls", "--exclude-env-file", ".env.local");
        yield return Case(two, null, "ls", ".", "--format", "tree");
        yield return Case(none, null, "ls");
        yield return Case(none, null, "ls", "nope-dir");
        yield return Case(two, null, "del", "FOO");
        yield return Case(two, null, "del", "NOPE");
        yield return Case(two, null, "del", "FOO", "-f", ".env.local");
        yield return Case(none, null, "del", "FOO");
        yield return Case(two, null, "del", "FOO", "-v");

        // encrypt / decrypt
        yield return Case(plain, null, "encrypt");
        yield return Case(plain, null, "encrypt", "-v");
        yield return Case(plain, null, "encrypt", "--stdout");
        yield return Case(plain, null, "encrypt", "-k", "HELLO");
        yield return Case(plain, null, "encrypt", "-ek", "NOPE");
        yield return Case(plain, null, "encrypt", "-ek", "FOO");
        yield return Case(plain, null, "encrypt", "--no-create");
        yield return Case(encrypted, null, "encrypt");
        yield return Case(none, null, "encrypt");
        yield return Case(none, null, "encrypt", "-f", ".nope");

        // encrypt --with (2.35.0): a public key created elsewhere
        string withKey = FixedKeypairFixture.PublicKey;
        string otherKey = "0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798";
        Dictionary<string, string> encryptedNoKey = Files(".env", "HELLO=\"encrypted:BAAA\"\nFOO=bar\n");
        Dictionary<string, string> plainWithKey = Files(".env", "DOTENV_PUBLIC_KEY=\"" + withKey + "\"\nHELLO=World\nFOO=bar\n");
        Dictionary<string, string> plainWithTwoKeys = Files(".env", "DOTENV_PUBLIC_KEY=\"" + withKey + "\"\nDOTENV_PUBLIC_KEY_X=\"" + otherKey + "\"\nHELLO=World\n");
        yield return Case(plain, null, "encrypt", "--with", withKey);
        yield return Case(plain, null, "encrypt", "--with", withKey, "-v");
        yield return Case(plain, null, "encrypt", "--with", withKey, "--stdout");
        yield return Case(plain, null, "encrypt", "--with", withKey.ToUpperInvariant());
        yield return Case(plain, null, "encrypt", "--with", withKey, "-k", "HELLO");
        yield return Case(plain, null, "encrypt", "--with", withKey, "-ek", "FOO");
        yield return Case(two, null, "encrypt", "--with", withKey, "-f", ".env", "-f", ".env.local");
        yield return Case(plain, null, "encrypt", "--with", "nope");
        yield return Case(plain, null, "encrypt", "--with", "");
        yield return Case(plain, null, "encrypt", "--with", "04" + new string('1', 128));
        yield return Case(plain, null, "encrypt", "--with", "02" + new string('0', 64));
        yield return Case(plain, null, "encrypt", "--with", withKey, "--stdout", "--no-create");
        yield return Case(none, null, "encrypt", "--with", withKey);
        yield return Case(none, null, "encrypt", "--with", withKey, "--no-create");
        yield return Case(encrypted, null, "encrypt", "--with", withKey);
        yield return Case(encrypted, null, "encrypt", "--with", otherKey);
        yield return Case(encryptedNoKey, null, "encrypt", "--with", withKey);
        yield return Case(plainWithKey, null, "encrypt", "--with", withKey);
        yield return Case(plainWithKey, null, "encrypt", "--with", otherKey);
        yield return Case(plainWithTwoKeys, null, "encrypt", "--with", withKey);
        yield return Case(encrypted, null, "decrypt");
        yield return Case(encrypted, null, "decrypt", "--stdout");
        yield return Case(encrypted, null, "decrypt", "-k", "HELLO");
        yield return Case(encrypted, null, "decrypt", "-ek", "HELLO");
        yield return Case(encrypted, null, "decrypt", "-ek", "NOPE");
        yield return Case(encrypted, null, "decrypt", "-v");
        yield return Case(plain, null, "decrypt");
        yield return Case(none, null, "decrypt");
        yield return Case(Files(".env", Encrypted), null, "decrypt");
        yield return Case(Files(".env", Encrypted), null, "decrypt", "--stdout");
        yield return Case(Files(".env", Encrypted), null, "get", "HELLO");

        // the "options: {...}" line of --debug is commander's opts(): defaults first, then what was given, in order
        yield return Case(two, null, "get", "--debug");
        yield return Case(two, null, "get", "FOO", "-f", ".env", ".env.local", "--debug");
        yield return Case(two, null, "get", "FOO", "-f", ".env,.env.local", "-f", " .env ", "--debug");
        yield return Case(two, null, "get", "--debug", "-e", "A=1", "B=2", "-e", "C=3", "--all", "--overload", "--strict");
        yield return Case(two, null, "get", "--debug", "-fk", "k1", "-fk", "k2", "-fk", "k3", "--format", "shell", "--pretty-print");
        yield return Case(two, null, "get", "--debug", "--convention", "nextjs", "--format", "eval");
        yield return Case(two, null, "get", "--debug", "--no-armor", "--no-native", "--no-1password", "--no-bitwarden");
        yield return Case(two, null, "get", "--debug", "--token", "tok");
        yield return Case(two, null, "get", "--debug", "--profile", "prod");
        yield return Case(two, null, "get", "--debug", "--ignore", "A", "B");
        yield return Case(two, null, "get", "--debug", "--file", ".env");
        yield return Case(two, null, "get", "--debug", "--env-file", ".env");
        yield return Case(two, new Dictionary<string, string> { ["DOTENV_CONVENTION"] = "flow", ["DOTENV_IGNORE"] = "X,Y", ["DOTENV_QUIET"] = "true" }, "run", "--debug", "--ignore", "Z", "--", "node", "-e", "1");
        yield return Case(two, new Dictionary<string, string> { ["DOTENV_CONFIG_CONVENTION"] = "nextjs", ["DOTENV_CONFIG_IGNORE"] = "X" }, "run", "-v", "--", "node", "-e", "1");
        yield return Case(two, null, "run", "--debug", "--mask", "--token", "abcdefghijklmnop", "-e", "SECRET=abcdefghijkl", "--", "node", "-e", "1");
        yield return Case(two, null, "run", "--debug", "--mask", "2", "--token", "abcdefghijklmnop", "--", "node", "-e", "1");
        yield return Case(two, null, "run", "--debug", "--profile", "p", "--convention", "flow", "-fk", "a", "-fk", "b", "--", "node", "-e", "1");
        yield return Case(two, null, "keypair", "--debug", "-f", ".env", "-fk", ".k", "--format", "shell");
        yield return Case(two, null, "keypair", "FOO", "--debug", "--pretty-print");
        yield return Case(two, null, "ls", "--debug");
        yield return Case(two, null, "ls", "--debug", "-f", ".env.local", "--exclude-env-file", "x", "y");
        yield return Case(two, null, "ls", "--debug", "--format", "json");
        yield return Case(two, null, "del", "FOO", "--debug", "-f", ".env", "-fk", ".k");
        yield return Case(two, null, "set", "FOO", "z", "--debug", "-f", ".env", "-fk", ".k", "--no-create");
        yield return Case(two, null, "set", "FOO", "z", "--debug", "--profile", "x", "--token", "t", "--no-armor");
        yield return Case(two, null, "encrypt", "--debug");
        yield return Case(two, null, "encrypt", "--debug", "-f", ".env", "-fk", ".k", "-k", "FOO", "-ek", "EXTRA", "--stdout", "--no-create");
        yield return Case(encrypted, null, "decrypt", "--debug", "-f", ".env", "-fk", ".env.keys", "-k", "HELLO", "-ek", "X", "--stdout");
        yield return Case(two, null, "gitignore", "--debug");
        yield return Case(two, null, "gitignore", "--debug", "--pattern", "a", "b");
        yield return Case(two, null, "genexample", "--debug");
        // genexample -f <directory> fails with an OS-specific read error (EISDIR text and code): not comparable
        yield return Case(two, null, "prebuild", "--debug");
        yield return Case(two, null, "precommit", "--debug");
        yield return Case(two, null, "precommit", "--debug", "--install");
        yield return Case(two, null, "ext", "scan", "--debug");
        yield return Case(two, null, "protect", "--debug", "--docker");
        yield return Case(two, null, "protect", "--debug", "--docker", "x");
        yield return Case(two, null, "native", "--debug");
        yield return Case(two, null, "native", "up", "--debug", "-f", ".env");
        yield return Case(two, null, "1password", "up", "--debug", "-f", ".env", "-fk", ".k");
        yield return Case(two, null, "bitwarden", "down", "--debug");
        yield return Case(two, null, "armor", "up", "--debug", "-f", ".env");
        yield return Case(two, null, "armor", "open", "--debug");
        // armor login/logout --debug print a Node stack trace after the error (JS runtime frames): not comparable
        yield return Case(two, null, "check", "--debug");
        yield return Case(two, null, "spec", "--debug");

        // check / primitives
        yield return Case(plain, null, "check");
        yield return Case(encrypted, null, "check");
        yield return Case(bad, null, "check");
        yield return Case(none, null, "check");
        yield return Case(plain, null, "check", "-f", ".nope");
        yield return Case(plain, null, "primitives", "check");
        yield return Case(plain, null, "primitives", "check", ".env");
        yield return Case(encrypted, null, "primitives", "check", ".env");
        yield return Case(bad, null, "primitives", "check", ".env");
        yield return Case(none, null, "primitives", "check", ".nope");
    }

    public static IEnumerable<object[]> ErrorCases()
    {
        Dictionary<string, string> two = Files(".env", "HELLO=World\nFOO=bar\n", ".env.local", "FOO=local\nEXTRA=1\n");
        object[] Case(params string[] args) => [two, args];

        yield return Case("genexample", "--debug", "-f", ".env", ".env.local", ".");
        yield return Case("armor", "login", "--debug", "--hostname", "h");
        yield return Case("armor", "logout", "--debug");
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task FailsLikeNode(Dictionary<string, string> files, string[] args)
    {
        await Compare(files, null, errorOnly: true, args);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesNode(Dictionary<string, string> files, Dictionary<string, string>? env, string[] args)
    {
        await Compare(files, env, args);
    }
}
