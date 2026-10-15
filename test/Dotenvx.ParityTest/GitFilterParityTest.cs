using System.Text;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): `dotenvx protect --git-process`
/// (src/cli/actions/protectProcess.js), the long-running Git filter-process (protocol v2) server. A scenario is the
/// byte stream Git would send on stdin; the exit code, the framed reply on stdout and the log on stderr must match Node.
/// </remarks>
public class GitFilterParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static string Pkt(string text)
    {
        return (Encoding.UTF8.GetByteCount(text) + 4).ToString("x4") + text;
    }

    private const string Flush = "0000";

    private static string Hello => Pkt("git-filter-client\n") + Pkt("version=2\n") + Flush;

    private static string Caps(params string[] capabilities)
    {
        return string.Concat(capabilities.Select(capability => Pkt($"capability={capability}\n"))) + Flush;
    }

    private static string Request(string command, string pathname, string content)
    {
        string body = "";
        for (int offset = 0; offset < content.Length; offset += 65516)
        {
            body += Pkt(content.Substring(offset, Math.Min(65516, content.Length - offset)));
        }

        return Pkt($"command={command}\n") + Pkt($"pathname={pathname}\n") + Flush + body + Flush;
    }

    private static string Session(string requests, params string[] capabilities)
    {
        return Hello + Caps(capabilities.Length == 0 ? ["clean", "smudge"] : capabilities) + requests;
    }

    private static readonly Dictionary<string, string> Inputs = new()
    {
        ["empty stdin"] = "",
        ["handshake only"] = Hello,
        ["handshake then eof"] = Session(""),
        ["encrypted env"] = Session(Request("clean", ".env", FixedKeypairFixture.EncryptedEnv)),
        ["plaintext env"] = Session(Request("clean", ".env", "HELLO=World\n")),
        ["plaintext then encrypted"] = Session(
            Request("clean", ".env", "HELLO=World\n") + Request("clean", "a/.env.prod", FixedKeypairFixture.EncryptedEnv)),
        ["two clean files"] = Session(
            Request("clean", ".env", FixedKeypairFixture.EncryptedEnv) + Request("clean", "sub/.env.other", FixedKeypairFixture.EncryptedEnv)),
        ["exempt plaintext"] = Session(Request("clean", ".env.example", "HELLO=World\n")),
        ["nested exempt"] = Session(Request("clean", "dir/.env.vault", "HELLO=World\n")),
        ["not an env file"] = Session(Request("clean", "src/readme.txt", "just text\n")),
        ["private key file"] = Session(Request("clean", ".env.keys", "DOTENV_PRIVATE_KEY=abc\n")),
        ["nested private key file"] = Session(Request("clean", "x/.env.keys.bak", "DOTENV_PRIVATE_KEY=abc\n")),
        ["smudge passthrough"] = Session(Request("smudge", ".env", "HELLO=World\n")),
        ["smudge not offered"] = Session(Request("smudge", ".env", "HELLO=World\n"), "clean"),
        ["missing clean capability"] = Session("", "smudge"),
        ["unknown command"] = Session(Request("frobnicate", ".env", "x\n")),
        ["missing pathname"] = Session(Pkt("command=clean\n") + Flush + Flush),
        ["bad handshake"] = Pkt("git-filter-client\n") + Pkt("version=3\n") + Flush,
        ["wrong client"] = Pkt("something-else\n") + Pkt("version=2\n") + Flush,
        ["non hex header"] = "zzzz",
        ["length below header"] = "0003",
        ["length above max"] = "fff1",
        ["incomplete packet"] = "0010abc",
        ["incomplete trailing header"] = Hello + "00",
        ["large content"] = Session(Request("clean", ".env.big", FixedKeypairFixture.EncryptedEnv + "# " + new string('x', 70000) + "\n")),
        ["large plaintext"] = Session(Request("clean", ".env", "HELLO=" + new string('x', 70000) + "\n")),
        ["unquoted path in message"] = Session(Request("clean", "my dir/.env", "HELLO=World\n")),
        ["quote in path"] = Session(Request("clean", "it's/.env", "HELLO=World\n")),
        ["unicode path"] = Session(Request("clean", "café/.env", "HELLO=World\n")),
        ["control char path"] = Session(Request("clean", "a\tb/.env", "HELLO=World\n")),
        ["empty content"] = Session(Request("clean", ".env", "")),
        ["malformed env"] = Session(Request("clean", ".env", "=\n\"unterminated\n"))
    };

    public static IEnumerable<object[]> Cases() => Inputs.Keys.Select(name => new object[] { name });

    private string Normalise(string text)
    {
        return text.Replace(WorkDir.Replace('\\', '/'), "<dir>").Replace(WorkDir, "<dir>").Replace("\r\n", "\n");
    }

    private async Task<string> Run(bool node, string input)
    {
        string project = Path.Combine(WorkDir, node ? "node" : "port");
        Directory.CreateDirectory(project);
        var env = new Dictionary<string, string> { ["DOTENVX_CONFIG"] = Path.Combine(WorkDir, "home") };
        string[] args = ["protect", "--git-process"];
        CliResult result = node
            ? await Fixture.RunNodeWithInputAsync(project, env, input, args)
            : await Fixture.RunDotnetWithInputAsync(project, env, input, args);
        return $"exit {result.ExitCode}\nout: {Normalise(result.StandardOutput.Replace(project, "<dir>"))}\nerr: {Normalise(result.StandardError.Replace(project, "<dir>"))}";
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesNode(string name)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string input = Inputs[name];
        string node = await Run(true, input);
        string dotnet = await Run(false, input);

        dotnet.ShouldBe(node, name);
    }
}
