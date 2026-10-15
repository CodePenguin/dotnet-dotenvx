using System.Text;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test. The macOS provider shells out to the absolute path `/usr/bin/security`, so a fake cannot be put
/// on PATH like `secret-tool` or `powershell`. Instead the same sequence of get/set/delete calls is run through
/// upstream's helpers/macosKeychain.js (with `child_process.execFileSync` redirected to the fake) and through
/// <see cref="NativeStore.MacosKeychain" /> (with <see cref="SecretCli.Exec" /> redirected the same way), against
/// test/fakes/security.cs installed as `security`. Both must make the same calls and report the same results, so
/// the macOS command lines and messages are checked on every platform.
/// </remarks>
public class MacosKeychainParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string SecurityBin = "/usr/bin/security";

    private static readonly (string Op, string Key, string Value)[] Sequence =
    [
        ("get", "k1", ""),
        ("delete", "k1", ""),
        ("set", "k1", "secret-one"),
        ("get", "k1", ""),
        ("set", "k1", "secret-two"),
        ("get", "k1", ""),
        ("set", "k2", "  padded  "),
        ("get", "k2", ""),
        ("set", "k3", "with space and \"quotes\" 'single' $HOME"),
        ("get", "k3", ""),
        ("set", "k4", "ünï 日本語 🔑"),
        ("get", "k4", ""),
        ("set", "03abc", "hex-like"),
        ("get", "03abc", ""),
        ("delete", "k1", ""),
        ("delete", "k1", ""),
        ("get", "k1", ""),
        ("get", "k2", ""),
        ("delete", "k2", ""),
        ("delete", "k3", ""),
        ("delete", "k4", ""),
        ("delete", "03abc", ""),
        ("get", "03abc", "")
    ];

    private string FakeDirectory(string name) => Path.Combine(WorkDir, name);

    private string InstallFake(string name)
    {
        string bin = Path.Combine(FakeDirectory(name), "bin");
        FakeCliInstaller.InstallFrom(bin, "security", "security");
        Directory.CreateDirectory(Path.Combine(FakeDirectory(name), "vault"));

        return Path.Combine(bin, "security" + (OperatingSystem.IsWindows() ? ".exe" : ""));
    }

    private static string Report(IEnumerable<(string Op, string Key, string Value, string Outcome)> results)
    {
        return string.Join("\n", results.Select(r => $"{r.Op} {r.Key} -> {r.Outcome}"));
    }

    [Fact]
    public async Task KeychainCallsAndResultsMatchNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string nodeFake = InstallFake("node");
        string dotnetFake = InstallFake("dotnet");

        // Node: macosKeychain.js with its hard-coded binary redirected to the fake
        string script = $$"""
                          const cp = require('child_process')
                          const real = cp.execFileSync
                          const fake = {{JsonSerializer.Serialize(nodeFake)}}
                          cp.execFileSync = (bin, args, opts) => real(bin === '{{SecurityBin}}' ? fake : bin, args, opts)
                          const realSpawn = cp.spawnSync
                          cp.spawnSync = (bin, args, opts) => realSpawn(bin === '{{SecurityBin}}' ? fake : bin, args, opts)
                          const keychain = require('./src/lib/helpers/macosKeychain')
                          const sequence = {{JsonSerializer.Serialize(Sequence.Select(s => new[] { s.Op, s.Key, s.Value }))}}
                          const lines = []
                          for (const [op, key, value] of sequence) {
                            let outcome
                            try {
                              if (op === 'get') outcome = 'ok <' + keychain.get(key) + '>'
                              else if (op === 'set') { keychain.set(key, value, 'label ' + key); outcome = 'ok' }
                              else { keychain.delete(key); outcome = 'ok' }
                            } catch (e) {
                              outcome = 'error ' + String(e.message).split(fake).join('{{SecurityBin}}')
                            }
                            lines.push(op + ' ' + key + ' -> ' + outcome)
                          }
                          process.stdout.write(lines.join('\n'))
                          """;
        script = script.Replace("\r\n", "\n");
        CliResult node = await Fixture.RunNodeScriptAsync(script,
            new Dictionary<string, string> { ["FAKE_SECURITY_DIR"] = Path.Combine(FakeDirectory("node"), "vault") });

        node.ExitCode.ShouldBe(0, node.StandardError);

        // .NET: NativeStore.MacosKeychain with the process seam redirected to the fake
        var results = new List<(string Op, string Key, string Value, string Outcome)>();
        var original = SecretCli.Exec;
        var originalWithInput = SecretCli.ExecWithInput;
        var originalFull = SecretCli.ExecFull;
        var fakeEnv = new Dictionary<string, string> { ["FAKE_SECURITY_DIR"] = Path.Combine(FakeDirectory("dotnet"), "vault") };
        try
        {
            SecretCli.Exec = (command, args, env) => original(command == SecurityBin ? dotnetFake : command, args, fakeEnv);
            SecretCli.ExecWithInput = (command, args, input) =>
                originalFull(command == SecurityBin ? dotnetFake : command, args, fakeEnv, input);
            var keychain = new NativeStore.MacosKeychain();
            foreach ((string op, string key, string value) in Sequence)
            {
                string outcome;
                try
                {
                    switch (op)
                    {
                        case "get":
                            outcome = "ok <" + keychain.Get(key) + ">";
                            break;
                        case "set":
                            keychain.Set(key, value, "label " + key);
                            outcome = "ok";
                            break;
                        default:
                            keychain.Delete(key);
                            outcome = "ok";
                            break;
                    }
                }
                catch (Exception e)
                {
                    outcome = "error " + e.Message;
                }

                results.Add((op, key, value, outcome));
            }
        }
        finally
        {
            SecretCli.Exec = original;
            SecretCli.ExecWithInput = originalWithInput;
        }

        Report(results).ShouldBe(node.StandardOutput.Replace("\r\n", "\n"));

        string nodeCalls = ReadCalls("node");
        string dotnetCalls = ReadCalls("dotnet");
        dotnetCalls.ShouldBe(nodeCalls);
        nodeCalls.ShouldContain("add-generic-password -U -s dotenvx -a k1 -l label k1 -w secret-one");
    }

    private string ReadCalls(string name)
    {
        string file = Path.Combine(FakeDirectory(name), "vault", "calls.log");

        return File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8).Replace("\r\n", "\n") : "";
    }
}
