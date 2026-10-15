using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): `dotenvx ext <command> [args...]` hands the arguments to a
/// `dotenvx-ext-<command>` program on PATH (src/lib/helpers/executeExtension.js). The fake of test/fakes/extension.cs stands in for the
/// extension; exit code, stdout and stderr of the Node CLI and the .NET CLI must match.
/// </remarks>
public class ExtensionParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static readonly Dictionary<string, (string[] Args, Dictionary<string, string> Env, string[] Installed)> Cases = new()
    {
        ["no command"] = (["ext"], [], ["hello"]),
        ["help flag"] = (["ext", "--help"], [], ["hello"]),
        ["extension runs"] = (["ext", "hello"], [], ["hello"]),
        ["forwarded args"] = (["ext", "hello", "a", "--b", "c=d", "-e"], [], ["hello"]),
        ["forwarded dashes"] = (["ext", "hello", "--", "x"], [], ["hello"]),
        ["flags before command"] = (["ext", "--debug", "hello", "x"], [], ["hello"]),
        ["debug lines"] = (["ext", "hello", "x", "--debug"], [], ["hello"]),
        ["exit code"] = (["ext", "hello"], new() { ["FAKEEXT_EXIT"] = "3" }, ["hello"]),
        ["stderr"] = (["ext", "hello"], new() { ["FAKEEXT_ERR"] = "oops", ["FAKEEXT_EXIT"] = "1" }, ["hello"]),
        ["unknown command"] = (["ext", "nope"], [], ["hello"]),
        ["unknown with args"] = (["ext", "nope", "a", "--b"], [], ["hello"]),
        ["vault not installed"] = (["ext", "vault"], [], ["hello"]),
        ["vault with args"] = (["ext", "vault", "encrypt", "-f", "x"], [], ["hello"]),
        ["vault installed"] = (["ext", "vault", "encrypt"], [], ["vault"]),
        ["unknown installed elsewhere"] = (["ext", "other"], [], ["hello"])
    };

    public static IEnumerable<object[]> Names() => Cases.Keys.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Names))]
    public async Task MatchesNode(string name)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (string[] args, Dictionary<string, string> extra, string[] installed) = Cases[name];
        string bin = Path.Combine(Path.GetTempPath(), "dotenvx-ext-" + Guid.NewGuid().ToString("N"));
        try
        {
            FakeCliInstaller.InstallFrom(bin, "extension", [.. installed.Select(command => "dotenvx-ext-" + command)]);

            // executeExtension.js puts "<cwd>/node_modules/.bin:" in front of PATH, which swallows the first entry on Windows: keep a decoy there
            string decoy = Path.Combine(bin, "decoy");
            Directory.CreateDirectory(decoy);
            var env = new Dictionary<string, string>
            {
                ["PATH"] = decoy + Path.PathSeparator + bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                ["DOTENVX_NO_NATIVE"] = "true"
            };
            foreach ((string key, string value) in extra)
            {
                env[key] = value;
            }

            CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);
            CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);

            string Describe(CliResult result) => $"exit {result.ExitCode}\nout:\n{result.StandardOutput.Replace("\r\n", "\n")}\nerr:\n{result.StandardError.Replace("\r\n", "\n")}";

            Describe(dotnet).ShouldBe(Describe(node), name);
        }
        finally
        {
            try
            {
                Directory.Delete(bin, true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
