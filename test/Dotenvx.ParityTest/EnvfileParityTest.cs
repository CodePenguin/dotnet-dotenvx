using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): the Envfile policy (src/lib/envfile, services/validate.js, the
/// `strict`/`redacted`/`encrypted` rules) was only exercised with malformed Envfiles. Every Envfile fixture runs through every
/// command that consults it; exit code, stdout, stderr and the resulting file tree must match the real Node CLI.
/// </remarks>
public class EnvfileParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string Plain = "HELLO=World\nFOO=bar\nSECRET_TOKEN=abc123\n";

    private static readonly Dictionary<string, string[]> Fixtures = new()
    {
        ["declared"] = [".env", Plain, "Envfile", "env \"HELLO\"\nenv \"FOO\"\n"],
        ["missing required"] = [".env", Plain, "Envfile", "env \"HELLO\"\nenv \"MISSING\"\n"],
        ["missing optional"] = [".env", Plain, "Envfile", "env \"HELLO\"\nenv \"MISSING\", optional: true\n"],
        ["strict missing"] = [".env", Plain, "Envfile", "strict true\nenv \"MISSING\"\n"],
        ["strict ok"] = [".env", Plain, "Envfile", "strict true\nenv \"HELLO\"\nenv \"FOO\"\nenv \"SECRET_TOKEN\"\n"],
        ["types"] = [".env", "N=5\nU=notaurl\nP=99999\nE=blue\nEM=bad\n", "Envfile",
            "env \"N\", type: \"integer\", min: 1, max: 3\nenv \"U\", type: \"url\"\nenv \"P\", type: \"port\"\nenv \"E\", enum: [\"red\", \"green\"]\nenv \"EM\", type: \"email\"\n"],
        ["types ok"] = [".env", "N=2\nU=http://x.test\nP=8080\nE=red\nEM=a@b.co\n", "Envfile",
            "env \"N\", type: \"integer\", min: 1, max: 3\nenv \"U\", type: \"url\"\nenv \"P\", type: \"port\"\nenv \"E\", enum: [\"red\", \"green\"]\nenv \"EM\", type: \"email\"\n"],
        ["redacted false"] = [".env", Plain, "Envfile", "env \"HELLO\", redacted: false\nenv \"FOO\"\nenv \"SECRET_TOKEN\"\n"],
        ["encrypted required"] = [".env", Plain, "Envfile", "env \"HELLO\", encrypted: true\nenv \"FOO\", encrypted: false\n"],
        ["encrypted file"] = [".env", FixedKeypairFixture.EncryptedEnv, ".env.keys", FixedKeypairFixture.PrivateKeyFile, "Envfile", "env \"HELLO\"\n"],
        ["file block"] = [".env", Plain, ".env.production", "HELLO=prod\nSTRIPE=sk\n", "Envfile",
            "env \"HELLO\"\nfile \".env.production\" do\n  env \"STRIPE\", required: true\n  env \"PORT\", type: \"port\"\nend\n"],
        ["profiles"] = [".env", Plain, ".env.production", "HELLO=prod\n", "Envfile",
            "env \"HELLO\"\nprofile \"development\", files: [\".env\"]\nprofile \"production\", files: [\".env.production\"]\n"],
        ["malformed"] = [".env", Plain, "Envfile", "this is not valid ((\n"],
        ["duplicate"] = [".env", Plain, "Envfile", "env \"HELLO\"\nenv \"HELLO\"\n"],
        ["empty envfile"] = [".env", Plain, "Envfile", ""],
        ["comments only"] = [".env", Plain, "Envfile", "# nothing\n"],
        ["unspecced keys"] = [".env", Plain, "Envfile", "env \"HELLO\"\n"]
    };

    private static readonly Dictionary<string, string[]> Commands = new()
    {
        ["run"] = ["run", "--", "node", "-e", "console.log(process.env.HELLO, process.env.FOO, process.env.SECRET_TOKEN)"],
        ["run strict"] = ["run", "--strict", "--", "node", "-e", "console.log('ran')"],
        ["run redact"] = ["run", "--redact", "--", "node", "-e", "console.log('t=' + process.env.SECRET_TOKEN + ' h=' + process.env.HELLO)"],
        ["run profile"] = ["run", "--profile", "production", "--", "node", "-e", "console.log(process.env.HELLO)"],
        ["run file"] = ["run", "-f", ".env.production", "--", "node", "-e", "console.log(process.env.HELLO, process.env.STRIPE)"],
        ["check"] = ["check"],
        ["check file"] = ["check", "-f", ".env.production"],
        ["check profile"] = ["check", "--profile", "production"],
        ["check quiet"] = ["check", "-q"],
        ["set"] = ["set", "NEW", "value"],
        ["set plain"] = ["set", "HELLO", "changed", "--plain"],
        ["set existing"] = ["set", "FOO", "baz"],
        ["encrypt"] = ["encrypt"],
        ["get"] = ["get", "HELLO"],
        ["get all"] = ["get"],
        ["spec stdout"] = ["spec", "--stdout"]
    };

    public static IEnumerable<object[]> Cases() =>
        Fixtures.Keys.SelectMany(fixtureName => Commands.Keys.Select(command => new object[] { fixtureName, command }));

    private string Snapshot()
    {
        return string.Join("\n", Directory.EnumerateFiles(WorkDir, "*", SearchOption.AllDirectories).Order()
            .Select(file => $"--- {Path.GetRelativePath(WorkDir, file).Replace('\\', '/')}\n{File.ReadAllText(file)}"));
    }

    private string Describe(CliResult result)
    {
        string text = $"exit {result.ExitCode}\nout:\n{result.StandardOutput.Replace(WorkDir, "<dir>")}\nerr:\n{result.StandardError.Replace(WorkDir, "<dir>")}\nfiles:\n{Snapshot()}";

        // fresh keypairs and ciphertext are random
        text = System.Text.RegularExpressions.Regex.Replace(text, "encrypted:[A-Za-z0-9+/=]+", "encrypted:<x>");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\(0[23]\\w \\w{3}\\)", "(<id>)");
        return System.Text.RegularExpressions.Regex.Replace(text, "\\b[0-9a-f]{64,66}\\b", "<hex>");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesNode(string fixtureName, string commandName)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] fixtureFiles = Fixtures[fixtureName];
        string[] args = Commands[commandName];
        var env = new Dictionary<string, string> { ["DOTENVX_NO_NATIVE"] = "true" };

        void Setup()
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(WorkDir).ToList())
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    File.Delete(path);
                }
            }

            for (int i = 0; i < fixtureFiles.Length; i += 2)
            {
                WriteFile(fixtureFiles[i], fixtureFiles[i + 1]);
            }
        }

        Setup();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);
        string nodeDescription = Describe(node);

        Setup();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);

        Describe(dotnet).ShouldBe(nodeDescription, $"{fixtureName} / {commandName}");
    }
}
