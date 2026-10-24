using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using Sdk = global::Dotenvx.Dotenvx;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): the library API (src/lib/main.js: config, parse, get, set, ls)
/// is never reached by the CLI parity tests. Each scenario runs the same calls through the real Node library
/// (oracle/sdk.cjs) and through <see cref="Main" /> from the same on-disk state, and compares what each returned or threw, every
/// message it asked its logger to print (level and text), the process env it wrote to and the resulting file tree.
/// </remarks>
public class SdkParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string Plain = "HELLO=World\nFOO=bar\n";

    private static readonly string Encrypted = FixedKeypairFixture.EncryptedEnv;

    private static readonly string Keys = FixedKeypairFixture.PrivateKeyFile;

    private sealed class RecordingLogger(JsonArray sink) : IDotenvxLogger
    {
        private void Add(string level, string message) => sink.Add(new JsonArray(level, message));

        public void Error(string message) => Add("error", message);

        public void InfoError(string message) => Add("infoerror", message);

        public void Warn(string message) => Add("warn", message);

        public void Success(string message) => Add("success", message);

        public void Info(string message) => Add("info", message);

        public void Help(string message) => Add("help", message);

        public void Verbose(string message) => Add("verbose", message);

        public void Debug(string message) => Add("debug", message);

        public void SetLevel(DotenvxLogLevel level)
        {
        }
    }

    private static JsonObject Call(string fn, JsonNode? args = null, JsonObject? options = null, bool defaultLogger = false)
    {
        var call = new JsonObject { ["fn"] = fn, ["args"] = args ?? new JsonArray() };
        if (defaultLogger)
        {
            // no logger in the options: the library prints through its own, and stdout/stderr are compared
            call["defaultLogger"] = true;
        }

        if (options is not null)
        {
            call["options"] = options;
        }

        return call;
    }

    private static JsonArray Args(params string?[] values) => new([.. values.Select(value => (JsonNode?)(value is null ? null : JsonValue.Create(value)))]);

    private static JsonObject Opts(string json) => JsonNode.Parse(json)!.AsObject();

    public static IEnumerable<object[]> Cases()
    {
        Dictionary<string, string> plain = new() { [".env"] = Plain };
        Dictionary<string, string> two = new() { [".env"] = "HELLO=World\nFOO=bar\n", [".env.local"] = "FOO=local\nEXTRA=1\n" };
        Dictionary<string, string> encrypted = new() { [".env"] = Encrypted, [".env.keys"] = Keys };
        Dictionary<string, string> none = [];

        object[] Scenario(string name, Dictionary<string, string> files, Dictionary<string, string>? env, params JsonObject[] calls) =>
            [name, files, env!, new JsonArray([.. calls.Select(call => (JsonNode?)call.DeepClone())]).ToJsonString()];

        // config
        yield return Scenario("config plain", plain, null, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config path array", two, null, Call("config", null, Opts("""{"processEnv":{},"path":[".env",".env.local"]}""")));
        yield return Scenario("config path string", two, null, Call("config", null, Opts("""{"processEnv":{},"path":".env, .env.local"}""")));
        yield return Scenario("config overload", two, null, Call("config", null, Opts("""{"processEnv":{"FOO":"x"},"path":[".env",".env.local"],"overload":true}""")));
        yield return Scenario("config override alias", two, null, Call("config", null, Opts("""{"processEnv":{"FOO":"x"},"path":[".env",".env.local"],"override":true}""")));
        yield return Scenario("config pre-existing wins", plain, null, Call("config", null, Opts("""{"processEnv":{"HELLO":"mine"}}""")));
        yield return Scenario("config missing file", none, null, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config missing file strict", none, null, Call("config", null, Opts("""{"processEnv":{},"strict":true}""")));
        yield return Scenario("config missing file ignored", none, null, Call("config", null, Opts("""{"processEnv":{},"ignore":["MISSING_ENV_FILE"]}""")));
        yield return Scenario("config missing convention", none, null, Call("config", null, Opts("""{"processEnv":{},"convention":"nextjs"}""")));
        yield return Scenario("config convention nextjs", two, null, Call("config", null, Opts("""{"processEnv":{},"convention":"nextjs"}""")));
        yield return Scenario("config convention flow", two, null, Call("config", null, Opts("""{"processEnv":{},"convention":"flow"}""")));
        yield return Scenario("config profile", new() { [".env.prod"] = "A=1\n" }, null, Call("config", null, Opts("""{"processEnv":{},"profile":"prod"}""")));
        yield return Scenario("config debug", plain, null, Call("config", null, Opts("""{"processEnv":{},"debug":true}""")));
        yield return Scenario("config verbose", plain, null, Call("config", null, Opts("""{"processEnv":{},"verbose":true}""")));
        yield return Scenario("config quiet", plain, null, Call("config", null, Opts("""{"processEnv":{},"quiet":true}""")));
        yield return Scenario("config logLevel", plain, null, Call("config", null, Opts("""{"processEnv":{},"logLevel":"warn"}""")));
        yield return Scenario("config mask", plain, null, Call("config", null, Opts("""{"processEnv":{},"mask":true}""")));
        yield return Scenario("config mask number", plain, null, Call("config", null, Opts("""{"processEnv":{},"mask":2}""")));
        yield return Scenario("config encrypted", encrypted, null, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config encrypted no keys", new() { [".env"] = Encrypted }, null, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config encrypted strict no keys", new() { [".env"] = Encrypted }, null, Call("config", null, Opts("""{"processEnv":{},"strict":true}""")));
        yield return Scenario("config envKeysFile", new() { [".env"] = Encrypted, ["custom.keys"] = Keys }, null, Call("config", null, Opts("""{"processEnv":{},"envKeysFile":"custom.keys"}""")));
        yield return Scenario("config key from processEnv", new() { [".env"] = Encrypted }, null,
            Call("config", null, Opts("{\"processEnv\":{\"DOTENV_PRIVATE_KEY\":\"" + FixedKeypairFixture.PrivateKey + "\"}}")));
        yield return Scenario("config malformed", new() { [".env"] = "A=1\nthis is not valid\n" }, null, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config empty", new() { [".env"] = "" }, null, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config process env", plain, new() { ["SDKTEST_X"] = "1" }, Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("config two calls", two, null,
            Call("config", null, Opts("""{"processEnv":{"S":"1"},"path":".env"}""")),
            Call("config", null, Opts("""{"processEnv":{"S":"1"},"path":".env.local"}""")));

        // config with explicit envs (documented option; sync resolver paths of envs.js)
        yield return Scenario("config envs file", two, null, Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"envFile","value":".env.local"}]}""")));
        yield return Scenario("config envs missing file", none, null, Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"envFile","value":"nope.env"}]}""")));
        yield return Scenario("config envs directory", new() { ["dir/x"] = "A=1\n" }, null, Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"envFile","value":"dir"}]}""")));
        yield return Scenario("config envs string", none, null, Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"env","value":"A=1\nB=two"}]}""")));
        yield return Scenario("config envs string overload", none, null,
            Call("config", null, Opts("""{"processEnv":{"A":"mine"},"overload":true,"envs":[{"type":"env","value":"A=1"}]}""")));
        yield return Scenario("config envs string existing", none, null,
            Call("config", null, Opts("""{"processEnv":{"A":"mine"},"envs":[{"type":"env","value":"A=1"}]}""")));
        yield return Scenario("config envs encrypted string no key", none, null,
            Call("config", null, Opts("{\"processEnv\":{},\"envs\":[{\"type\":\"env\",\"value\":" + System.Text.Json.JsonSerializer.Serialize(Encrypted) + "}]}")));
        yield return Scenario("config envs encrypted string key", none, null,
            Call("config", null, Opts("{\"processEnv\":{\"DOTENV_PRIVATE_KEY\":\"" + FixedKeypairFixture.PrivateKey + "\"},\"envs\":[{\"type\":\"env\",\"value\":" + System.Text.Json.JsonSerializer.Serialize(Encrypted) + "}]}")));
        yield return Scenario("config envs mixed", two, null,
            Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"envFile","value":".env"},{"type":"env","value":"EXTRA=2\nFOO=string"},{"type":"envFile","value":".env.local"}]}""")));
        yield return Scenario("config envs strict missing", none, null,
            Call("config", null, Opts("""{"processEnv":{},"strict":true,"envs":[{"type":"envFile","value":"nope.env"}]}""")));
        yield return Scenario("config envs encrypted file", encrypted, null,
            Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"envFile","value":".env"}]}""")));
        yield return Scenario("config envs encrypted file no keys", new() { [".env"] = Encrypted }, null,
            Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"envFile","value":".env"}]}""")));

        // secret references resolved by the synchronous SDK path (resolveOnePassword.sync, resolveBitwardenPassword.sync)
        Dictionary<string, string> refs = new() { [".env"] = "A=bw://github/password\nB=plain\nC=op://vault/item/field\n" };
        Dictionary<string, string> Fakes(string clis, string session = "session-token") =>
            new() { ["FAKE_CLIS"] = clis, ["BW_SESSION"] = session };
        yield return Scenario("refs both resolved", refs, Fakes("op,bw"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs bw no session", refs, Fakes("op,bw", ""), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs bw missing cli", refs, Fakes("op"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs op missing cli", refs, Fakes("bw"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs no clis", refs, Fakes(""), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs bw missing item", new() { [".env"] = "A=bw://nope/password\nB=ok\n" }, Fakes("bw"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs op missing item", new() { [".env"] = "A=op://vault/missing/field\nB=ok\n" }, Fakes("op"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs bw unsupported field", new() { [".env"] = "A=bw://github/shoesize\n" }, Fakes("bw"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs bw malformed", new() { [".env"] = "A=bw://\nB=bw://onlyitem\n" }, Fakes("bw"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs disabled by option", refs, Fakes("op,bw"), Call("config", null, Opts("""{"processEnv":{},"noBitwarden":true,"no1Password":true}""")));
        yield return Scenario("refs only bw disabled", refs, Fakes("op,bw"), Call("config", null, Opts("""{"processEnv":{},"noBitwarden":true}""")));
        yield return Scenario("refs disabled by env", refs, new() { ["FAKE_CLIS"] = "op,bw", ["BW_SESSION"] = "s", ["DOTENVX_NO_1PASSWORD"] = "true", ["DOTENVX_NO_BITWARDEN"] = "true" },
            Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("refs in envs string", none, Fakes("op,bw"),
            Call("config", null, Opts("""{"processEnv":{},"envs":[{"type":"env","value":"A=bw://github/password\nC=op://vault/item/field"}]}""")));
        yield return Scenario("refs get key", refs, Fakes("op,bw"), Call("get", Args("A")), Call("get", Args("C")), Call("get", Args("B")));
        yield return Scenario("refs get all", refs, Fakes("op,bw"), Call("get", Args((string?)null), Opts("""{"all":true}""")));
        yield return Scenario("refs get missing session", refs, Fakes("op,bw", ""), Call("get", Args("A")));

        // Armor: the synchronous decryptor/provider worker (decryptors/index.js sync, providers.sync) against a fake API
        Dictionary<string, string> armored = new() { [".env"] = Encrypted };
        Dictionary<string, string> Armor(string mode) => new() { ["ARMOR"] = mode };
        const string Tok = """{"processEnv":{},"token":"tok_abc1234567890"}""";
        yield return Scenario("armor keyring config", armored, Armor("keyring"), Call("config", null, Opts(Tok)));
        yield return Scenario("armor keyring get", armored, Armor("keyring"), Call("get", Args("HELLO"), Opts("""{"token":"tok_abc1234567890"}""")));
        yield return Scenario("armor keyring get all", armored, Armor("keyring"), Call("get", Args((string?)null), Opts("""{"token":"tok_abc1234567890","all":true}""")));
        yield return Scenario("armor empty keyring", armored, Armor("empty"), Call("config", null, Opts(Tok)));
        yield return Scenario("armor keyring error", armored, Armor("error"), Call("config", null, Opts(Tok)));
        yield return Scenario("armor server side decrypt config", armored, Armor("server"), Call("config", null, Opts(Tok)));
        yield return Scenario("armor server side decrypt get", armored, Armor("server"), Call("get", Args("HELLO"), Opts("""{"token":"tok_abc1234567890"}""")));
        yield return Scenario("armor disabled by option", armored, Armor("keyring"),
            Call("config", null, Opts("""{"processEnv":{},"token":"tok_abc1234567890","noArmor":true}""")));
        yield return Scenario("armor session without token", armored, Armor("keyring"), Call("config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("armor plain file untouched", new() { [".env"] = Plain }, Armor("keyring"), Call("config", null, Opts(Tok)));

        // parse
        yield return Scenario("parse plain", none, null, Call("parse", Args("A=1\nB=two\n")));
        yield return Scenario("parse quotes", none, null, Call("parse", Args("A=\"x y\"\nB='z'\nC=`k`\n# c\nD=1 # trailing\n")));
        yield return Scenario("config override", two, null, Call("config", null, Opts("""{"processEnv":{"FOO":"x"},"path":[".env",".env.local"],"override":true}""")));
        yield return Scenario("parse override", none, null, Call("parse", Args("A=1\n"), Opts("""{"processEnv":{"A":"mine"},"override":true}""")));
        yield return Scenario("get override is ignored", two, null, Call("get", Args((string?)null), Opts("""{"path":[".env",".env.local"],"override":true}""")));
        yield return Scenario("config native false", two, null, Call("config", null, Opts("""{"processEnv":{"X":"1"},"path":[".env",".env.local"],"native":false}""")));
        yield return Scenario("parse overload", none, null, Call("parse", Args("A=1\n"), Opts("""{"processEnv":{"A":"mine"},"overload":true}""")));
        yield return Scenario("parse existing", none, null, Call("parse", Args("A=1\n"), Opts("""{"processEnv":{"A":"mine"}}""")));
        yield return Scenario("parse encrypted no key", none, null, Call("parse", Args(Encrypted)));
        yield return Scenario("parse encrypted with key", none, null,
            Call("parse", Args(Encrypted), Opts("{\"privateKey\":\"" + FixedKeypairFixture.PrivateKey + "\"}")));
        yield return Scenario("parse encrypted wrong key", none, null,
            Call("parse", Args(Encrypted), Opts("""{"privateKey":"0000000000000000000000000000000000000000000000000000000000000001"}""")));
        yield return Scenario("parse encrypted ignored", none, null,
            Call("parse", Args(Encrypted), Opts("""{"ignore":["MISSING_PRIVATE_KEY"]}""")));
        yield return Scenario("parse empty", none, null, Call("parse", Args("")));

        // get
        yield return Scenario("get key", plain, null, Call("get", Args("HELLO")));
        yield return Scenario("get missing key", plain, null, Call("get", Args("NOPE")));
        yield return Scenario("get all", two, null, Call("get", Args((string?)null), Opts("""{"path":[".env",".env.local"]}""")));
        yield return Scenario("get format eval", plain, null, Call("get", Args((string?)null), Opts("""{"format":"eval"}""")));
        yield return Scenario("get format eval-export", plain, null, Call("get", Args((string?)null), Opts("""{"format":"eval-export"}""")));
        yield return Scenario("get format shell", plain, null, Call("get", Args((string?)null), Opts("""{"format":"shell"}""")));
        yield return Scenario("get format colon", plain, null, Call("get", Args((string?)null), Opts("""{"format":"colon"}""")));
        yield return Scenario("get format json", plain, null, Call("get", Args((string?)null), Opts("""{"format":"json"}""")));
        yield return Scenario("get mask", plain, null, Call("get", Args("HELLO"), Opts("""{"mask":true}""")));
        yield return Scenario("get mask number", plain, null, Call("get", Args((string?)null), Opts("""{"mask":2}""")));
        yield return Scenario("get encrypted", encrypted, null, Call("get", Args("HELLO")));
        yield return Scenario("get encrypted no keys", new() { [".env"] = Encrypted }, null, Call("get", Args("HELLO")));
        yield return Scenario("get encrypted no keys strict", new() { [".env"] = Encrypted }, null, Call("get", Args("HELLO"), Opts("""{"strict":true}""")));
        yield return Scenario("get encrypted no keys ignored", new() { [".env"] = Encrypted }, null,
            Call("get", Args("HELLO"), Opts("""{"ignore":["MISSING_PRIVATE_KEY"]}""")));
        yield return Scenario("get missing file", none, null, Call("get", Args("HELLO")));
        yield return Scenario("get missing file strict", none, null, Call("get", Args("HELLO"), Opts("""{"strict":true}""")));
        yield return Scenario("get convention", two, null, Call("get", Args((string?)null), Opts("""{"convention":"nextjs"}""")));
        yield return Scenario("get overload", two, null, Call("get", Args((string?)null), Opts("""{"path":[".env",".env.local"],"overload":true}""")));
        yield return Scenario("get all pre-existing", plain, new() { ["SDKTEST_Y"] = "1" }, Call("get", Args((string?)null), Opts("""{"all":true}""")));
        yield return Scenario("get profile", new() { [".env.prod"] = "A=1\n" }, null, Call("get", Args("A"), Opts("""{"profile":"prod"}""")));
        yield return Scenario("get envKeysFile", new() { [".env"] = Encrypted, ["custom.keys"] = Keys }, null,
            Call("get", Args("HELLO"), Opts("""{"envKeysFile":"custom.keys"}""")));

        // set
        yield return Scenario("set new", plain, null, Call("set", Args("A", "1")));
        yield return Scenario("set plain", plain, null, Call("set", Args("A", "1"), Opts("""{"plain":true}""")));
        yield return Scenario("set encrypt false", plain, null, Call("set", Args("A", "1"), Opts("""{"encrypt":false}""")));
        yield return Scenario("set no file", none, null, Call("set", Args("A", "1")));
        yield return Scenario("set create false", none, null, Call("set", Args("A", "1"), Opts("""{"create":false}""")));
        yield return Scenario("set unchanged", plain, null, Call("set", Args("HELLO", "World"), Opts("""{"plain":true}""")));
        yield return Scenario("set existing encrypted", encrypted, null, Call("set", Args("HELLO", "changed")));
        yield return Scenario("set path", two, null, Call("set", Args("A", "1"), Opts("""{"path":[".env",".env.local"]}""")));
        yield return Scenario("set path string", two, null, Call("set", Args("A", "1"), Opts("""{"path":".env.local"}""")));
        yield return Scenario("set envKeysFile", plain, null, Call("set", Args("A", "1"), Opts("""{"envKeysFile":"custom.keys"}""")));
        yield return Scenario("set profile", new() { [".env.prod"] = "A=1\n" }, null, Call("set", Args("B", "2"), Opts("""{"profile":"prod"}""")));
        yield return Scenario("set debug", plain, null, Call("set", Args("A", "1"), Opts("""{"debug":true}""")));
        yield return Scenario("set verbose", plain, null, Call("set", Args("A", "1"), Opts("""{"verbose":true}""")));
        yield return Scenario("set quiet", plain, null, Call("set", Args("A", "1"), Opts("""{"quiet":true}""")));
        yield return Scenario("set convention", plain, null, Call("set", Args("A", "1"), Opts("""{"convention":"nextjs"}""")));
        yield return Scenario("set twice", plain, null, Call("set", Args("A", "1")), Call("set", Args("A", "2")), Call("get", Args("A")));

        // ls
        yield return Scenario("ls default", two, null, Call("ls", Args(".")));
        yield return Scenario("ls filter", two, null, Call("ls", Args(".", ".env.local")));
        yield return Scenario("ls exclude", two, null, Call("ls", Args(".", ".env*", ".env.local")));
        yield return Scenario("ls missing dir", none, null, Call("ls", Args("nope")));

        // withEvents (no Armor login: the facade must behave exactly like the plain calls)
        yield return Scenario("events config", plain, null, Call("events.config", null, Opts("""{"processEnv":{}}""")));
        yield return Scenario("events config missing", none, null, Call("events.config", null, Opts("""{"processEnv":{},"strict":true}""")));
        yield return Scenario("events get", plain, null, Call("events.get", Args("HELLO")));
        yield return Scenario("events get missing key", plain, null, Call("events.get", Args("NOPE")));
        yield return Scenario("events get missing file", none, null, Call("events.get", Args("HELLO"), Opts("""{"path":"nope.env"}""")));
        yield return Scenario("events set", plain, null, Call("events.set", Args("NEW", "1")));
        yield return Scenario("events set error", none, null, Call("events.set", Args("NEW", "1"), Opts("""{"path":"nope.env","noCreate":true}""")));
        yield return Scenario("events sequence", plain, null,
            Call("events.set", Args("A", "1"), Opts("""{"plain":true}""")),
            Call("events.get", Args("A")),
            Call("events.config", null, Opts("""{"processEnv":{}}""")));

        // Envfile redaction of the process's own stdout/stderr (redactProcessOutput.js)
        const string Declared = "env \"HELLO\"\nenv \"FOO\"\n";
        Dictionary<string, string> withEnvfile = new() { [".env"] = Plain, ["Envfile"] = Declared };
        JsonObject Config() => Call("config", null, Opts("""{"processEnv":{}}"""));
        yield return Scenario("redact stdout", withEnvfile, null, Config(), Call("write", Args("stdout", "say World and bar now")));
        yield return Scenario("redact stderr", withEnvfile, null, Config(), Call("write", Args("stderr", "err World")));
        yield return Scenario("redact split secret", withEnvfile, null, Config(), Call("write", Args("stdout", "say Wor", "ld done")));
        yield return Scenario("redact held prefix", withEnvfile, null, Config(), Call("write", Args("stdout", "say Wor")));
        yield return Scenario("redact prefix not secret", withEnvfile, null, Config(), Call("write", Args("stdout", "say Wor", "ry")));
        yield return Scenario("redact many writes", withEnvfile, null, Config(), Call("write", Args("stdout", "W", "o", "r", "l", "d", "!")));
        yield return Scenario("redact before config", withEnvfile, null, Call("write", Args("stdout", "World")), Config(), Call("write", Args("stdout", "World")));
        yield return Scenario("redact public key unredacted", new() { [".env"] = Plain, ["Envfile"] = "env \"HELLO\", redacted: false\nenv \"FOO\"\n" }, null,
            Config(), Call("write", Args("stdout", "World bar")));
        yield return Scenario("redact no envfile", plain, null, Config(), Call("write", Args("stdout", "World bar")));
        yield return Scenario("redact empty envfile", new() { [".env"] = Plain, ["Envfile"] = "" }, null, Config(), Call("write", Args("stdout", "World bar")));
        yield return Scenario("redact second config adds secrets", new() { [".env"] = Plain, [".env.more"] = "MORE=secretvalue\n", ["Envfile"] = Declared + "env \"MORE\"\n" }, null,
            Call("config", null, Opts("""{"processEnv":{},"path":".env"}""")),
            Call("write", Args("stdout", "bar secretvalue")),
            Call("config", null, Opts("""{"processEnv":{},"path":".env.more"}""")),
            Call("write", Args("stdout", "bar secretvalue")));
        yield return Scenario("redact encrypted env", new() { [".env"] = Encrypted, [".env.keys"] = Keys, ["Envfile"] = "env \"HELLO\"\n" }, null,
            Config(), Call("write", Args("stdout", "World")));
        yield return Scenario("redact non ascii", new() { [".env"] = "HELLO=café\n", ["Envfile"] = "env \"HELLO\"\n" }, null, Config(), Call("write", Args("stdout", "a café b", "caf", "é")));

        // the default logger (main.js `logger`, used when the options carry none): what reaches stdout and stderr, colors included
        Dictionary<string, string> Color(string value) => new() { ["FORCE_COLOR"] = value };
        yield return Scenario("default logger config", plain, null, Call("config", null, Opts("""{"processEnv":{}}"""), true));
        yield return Scenario("default logger config quiet", plain, null, Call("config", null, Opts("""{"processEnv":{},"quiet":true}"""), true));
        yield return Scenario("default logger config verbose", plain, null, Call("config", null, Opts("""{"processEnv":{},"verbose":true}"""), true));
        yield return Scenario("default logger config debug", plain, null, Call("config", null, Opts("""{"processEnv":{},"debug":true}"""), true));
        yield return Scenario("default logger config logLevel warn", plain, null, Call("config", null, Opts("""{"processEnv":{},"logLevel":"warn"}"""), true));
        yield return Scenario("default logger config logLevel error", plain, null, Call("config", null, Opts("""{"processEnv":{},"logLevel":"error"}"""), true));
        yield return Scenario("default logger level persists", plain, null,
            Call("config", null, Opts("""{"processEnv":{},"debug":true}"""), true),
            Call("config", null, Opts("""{"processEnv":{}}"""), true),
            Call("config", null, Opts("""{"processEnv":{},"quiet":true}"""), true));
        yield return Scenario("default logger missing file", none, null, Call("config", null, Opts("""{"processEnv":{}}"""), true));
        yield return Scenario("default logger missing file strict", none, null, Call("config", null, Opts("""{"processEnv":{},"strict":true}"""), true));
        yield return Scenario("default logger encrypted no keys", new() { [".env"] = Encrypted }, null, Call("config", null, Opts("""{"processEnv":{}}"""), true));
        yield return Scenario("default logger malformed", new() { [".env"] = "A=1\nthis is not valid\n" }, null, Call("config", null, Opts("""{"processEnv":{}}"""), true));
        yield return Scenario("default logger pre-existing verbose", plain, null, Call("config", null, Opts("""{"processEnv":{"HELLO":"mine"},"verbose":true}"""), true));
        yield return Scenario("default logger parse", none, null, Call("parse", Args("A=1\n"), Opts("""{"processEnv":{"A":"mine"},"debug":true}"""), true));
        yield return Scenario("default logger get missing file", none, null, Call("get", Args("HELLO"), null, true));
        yield return Scenario("default logger get debug", plain, null, Call("get", Args("HELLO"), Opts("""{"debug":true}"""), true));
        yield return Scenario("default logger set", plain, null, Call("set", Args("A", "1"), null, true));
        yield return Scenario("default logger set verbose", plain, null, Call("set", Args("A", "1"), Opts("""{"verbose":true}"""), true));
        foreach (string depth in new[] { "0", "1", "2", "3" })
        {
            yield return Scenario($"default logger colors FORCE_COLOR={depth}", plain, Color(depth),
                Call("config", null, Opts("""{"processEnv":{},"debug":true}"""), true),
                Call("get", Args("HELLO"), Opts("""{"debug":true}"""), true));
            yield return Scenario($"default logger error colors FORCE_COLOR={depth}", new() { [".env"] = Encrypted }, Color(depth),
                Call("config", null, Opts("""{"processEnv":{}}"""), true),
                Call("config", null, Opts("""{"processEnv":{},"logLevel":"warn"}"""), true));
        }

        yield return Scenario("default logger NO_COLOR", plain, new() { ["NO_COLOR"] = "1" }, Call("config", null, Opts("""{"processEnv":{},"debug":true}"""), true));

        // getColor / bold: every color on both streams for each way the terminal's capability can be decided
        string[] colors = ["amber", "blue", "gray", "green", "olive", "orangered", "plum", "red", "electricblue", "dodgerblue", "nope"];
        Dictionary<string, Dictionary<string, string>> terminals = new()
        {
            ["plain"] = [],
            ["FORCE_COLOR=0"] = Color("0"),
            ["FORCE_COLOR=1"] = Color("1"),
            ["FORCE_COLOR=2"] = Color("2"),
            ["FORCE_COLOR=3"] = Color("3"),
            ["FORCE_COLOR=true"] = Color("true"),
            ["NO_COLOR"] = new() { ["NO_COLOR"] = "1" },
            ["NO_COLOR with FORCE_COLOR"] = new() { ["NO_COLOR"] = "1", ["FORCE_COLOR"] = "3" },
            ["NODE_DISABLE_COLORS"] = new() { ["NODE_DISABLE_COLORS"] = "1", ["FORCE_COLOR"] = "3" },
            ["COLORTERM truecolor"] = new() { ["COLORTERM"] = "truecolor", ["FORCE_COLOR"] = "1" },
            ["TERM xterm-256color"] = new() { ["TERM"] = "xterm-256color", ["FORCE_COLOR"] = "1" }
        };
        foreach ((string terminal, Dictionary<string, string> env) in terminals)
        {
            List<JsonObject> calls = [];
            foreach (string stream in new[] { "stdout", "stderr" })
            {
                calls.AddRange(colors.Select(color => Call("getColor", Args(color, stream, "text"))));
                calls.Add(Call("bold", Args("text", stream)));
            }

            yield return Scenario($"colors {terminal}", none, env, [.. calls]);
        }
    }

    private static string Normalise(string text, string workDir)
    {
        text = text.Replace(workDir.Replace('\\', '/'), "<dir>").Replace(workDir, "<dir>").Replace("\\\\", "\\");
        text = Regex.Replace(text, "\"DOTENVX_CONFIG\": \"[^\"]*\"", "\"DOTENVX_CONFIG\": \"<config>\"");
        text = Regex.Replace(text, @"[0-9a-f]{64,66}", "<hex>");
        text = Regex.Replace(text, @"encrypted:[A-Za-z0-9+/=]+", "encrypted:<cipher>");
        text = Regex.Replace(text, @"\([0-9A-F]{3} [0-9A-F]{3}\)", "(<fingerprint>)");
        text = Regex.Replace(text, @"\.\d{6,}'", "'");

        return text;
    }

    private string Snapshot()
    {
        var lines = new List<string>();
        foreach (string file in Directory.EnumerateFiles(WorkDir, "*", SearchOption.AllDirectories).Order())
        {
            string content = File.ReadAllText(file);
            content = Regex.Replace(content, "(PUBLIC_KEY[A-Za-z0-9_.]*=\")[0-9a-f]+\"", "$1<public>\"");
            content = Regex.Replace(content, "(PRIVATE_KEY[A-Za-z0-9_.]*=)[0-9a-f]+", "$1<private>");
            content = Regex.Replace(content, "encrypted:[A-Za-z0-9+/=]+", "encrypted:<cipher>");
            lines.Add($"--- {Path.GetRelativePath(WorkDir, file).Replace('\\', '/')}\n{content}");
        }

        return string.Join("\n", lines);
    }

    private void Setup(Dictionary<string, string> files)
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

    private static ColorStream Stream(JsonNode? node) => Str(node) == "stdout" ? ColorStream.StdOut : ColorStream.StdErr;

    private static string? Str(JsonNode? node) => node?.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>() : null;

    private static bool Flag(JsonObject options, string name) => options[name]?.GetValueKind() == System.Text.Json.JsonValueKind.True;

    private static T Map<T>(JsonObject options) where T : ConfigOptions, new()
    {
        var mapped = new T();
        ConfigOptions target = mapped;
        if (options["path"] is JsonArray array)
        {
            target.Path = [.. array.Select(item => item!.GetValue<string>())];
        }
        else if (Str(options["path"]) is { } path)
        {
            target.PathString = path;
        }

        if (Flag(options, "overload"))
        {
            target.Overload = true;
        }

        if (Flag(options, "override"))
        {
            target.Override = true;
        }

        if (options["native"] is not null)
        {
            target.Native = !Flag(options, "native") ? false : null;
        }


        if (options["processEnv"] is JsonObject processEnv)
        {
            target.ProcessEnv = processEnv.ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<string>());
        }

        if (options["envs"] is JsonArray envs)
        {
            target.Envs =
            [
                .. envs.Select(item => new EnvItem
                {
                    Type = item!["type"]!.GetValue<string>() == "env" ? EnvType.Env : EnvType.EnvFile,
                    Value = item["value"]!.GetValue<string>()
                })
            ];
        }

        if (options["ignore"] is JsonArray ignore)
        {
            target.Ignore = [.. ignore.Select(item => item!.GetValue<string>())];
        }

        target.Strict = options["strict"] is null ? null : Flag(options, "strict");
        target.EnvKeysFile = Str(options["envKeysFile"]);
        target.NoNative = options["noNative"] is null ? null : Flag(options, "noNative");
        target.No1Password = options["no1Password"] is null ? null : Flag(options, "no1Password");
        target.NoBitwarden = options["noBitwarden"] is null ? null : Flag(options, "noBitwarden");
        target.NoArmor = options["noArmor"] is null ? null : Flag(options, "noArmor");
        target.Token = Str(options["token"]);
        target.Profile = Str(options["profile"]);
        target.Convention = Str(options["convention"]);
        target.Debug = options["debug"] is null ? null : Flag(options, "debug");
        target.Quiet = options["quiet"] is null ? null : Flag(options, "quiet");
        target.Verbose = options["verbose"] is null ? null : Flag(options, "verbose");
        target.LogLevel = Str(options["logLevel"]);
        if (options["mask"] is { } mask)
        {
            target.Mask = mask.GetValueKind() == System.Text.Json.JsonValueKind.True ? true : mask.GetValue<int>();
        }

        if (mapped is GetOptions get)
        {
            get.All = options["all"] is null ? null : Flag(options, "all");
            get.Format = Str(options["format"]);
        }

        if (mapped is SetsOptions set)
        {
            set.Plain = options["plain"] is null ? null : Flag(options, "plain");
            set.Encrypt = options["encrypt"] is null ? null : Flag(options, "encrypt");
            set.NoCreate = options["create"]?.GetValueKind() == System.Text.Json.JsonValueKind.False ? true : null;
        }

        return mapped;
    }

    private static JsonNode? Dict(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var result = new JsonObject();
        foreach ((string key, string value) in pairs)
        {
            result[key] = value;
        }

        return result;
    }

    private static JsonObject? DescribeError(Exception? error)
    {
        if (error is null)
        {
            return null;
        }

        string? code = global::Dotenvx.Interop.DotenvxJs.Helpers.Helpers.ErrorCodeOf(error);
        string message = (error as DotenvxException)?.MessageWithHelp ?? error.Message;

        return new JsonObject { ["code"] = code, ["message"] = message };
    }

    private static void await_(Task task) => task.GetAwaiter().GetResult();

    private JsonObject RunPort(Dictionary<string, string>? env, JsonArray calls)
    {
        string previousDirectory = Directory.GetCurrentDirectory();
        var restore = new Dictionary<string, string?>();
        var effective = new Dictionary<string, string>
        {
            ["DOTENVX_NO_NATIVE"] = "true",
            ["DOTENVX_CONFIG"] = Path.Combine(WorkDir, "..", "dotenvx-sdk-config-" + Guid.NewGuid().ToString("N"))
        };
        foreach ((string name, string value) in env ?? [])
        {
            effective[name] = value;
        }

        foreach ((string name, string value) in effective)
        {
            restore[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        Directory.SetCurrentDirectory(WorkDir);
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        var capturedOut = new StringWriter();
        var capturedError = new StringWriter();
        RedactProcessOutput.Reset();
        Sdk.Logger.SetLevel(DotenvxLogLevel.Info); // the shared logger keeps its level for the life of the process
        Console.SetOut(capturedOut);
        Console.SetError(capturedError);
        var eventSdk = Sdk.WithEvents();
        try
        {
            var rows = new JsonArray();
            foreach (JsonNode? node in calls)
            {
                JsonObject call = node!.AsObject();
                string fn = call["fn"]!.GetValue<string>();
                JsonArray args = call["args"]!.AsArray();
                JsonObject options = call["options"]?.AsObject() ?? [];
                var messages = new JsonArray();
                var logger = new RecordingLogger(messages);
                bool defaultLogger = call["defaultLogger"]?.GetValue<bool>() == true;
                capturedOut.GetStringBuilder().Clear();
                capturedError.GetStringBuilder().Clear();
                var row = new JsonObject { ["fn"] = fn };
                IDictionary<string, string>? processEnv = null;
                try
                {
                    switch (fn)
                    {
                        case "config":
                            {
                                ConfigOptions mapped = Map<ConfigOptions>(options);
                                mapped.Logger = defaultLogger ? null : logger;
                                processEnv = mapped.ProcessEnv;
                                ConfigResult result = Sdk.Config(mapped);
                                row["result"] = new JsonObject { ["parsed"] = Dict(result.Parsed), ["error"] = DescribeError(result.Error) };
                                break;
                            }
                        case "parse":
                            {
                                var parseOptions = new ParseOptions
                                {
                                    Logger = defaultLogger ? null : logger,
                                    PrivateKey = Str(options["privateKey"]),
                                    Overload = Flag(options, "overload"),
                                    Override = Flag(options, "override"),
                                    Ignore = options["ignore"] is JsonArray ignore ? [.. ignore.Select(item => item!.GetValue<string>())] : null,
                                    ProcessEnv = options["processEnv"] is JsonObject penv
                                        ? penv.ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<string>())
                                        : null
                                };
                                processEnv = parseOptions.ProcessEnv?.ToDictionary();
                                row["result"] = new JsonObject { ["parsed"] = Dict(Sdk.Parse(Str(args[0]), parseOptions)) };
                                break;
                            }
                        case "get":
                            {
                                GetOptions mapped = Map<GetOptions>(options);
                                mapped.Logger = defaultLogger ? null : logger;
                                processEnv = mapped.ProcessEnv;
                                object? value = Sdk.Get(Str(args[0]), mapped);
                                JsonNode? shown = value switch
                                {
                                    null => new JsonObject { ["undefined"] = true },
                                    string text => text,
                                    IReadOnlyDictionary<string, string> dictionary => Dict(dictionary),
                                    IDictionary<string, string> dictionary => Dict(dictionary),
                                    _ => throw new InvalidOperationException(value.GetType().FullName)
                                };
                                row["result"] = new JsonObject { ["value"] = shown };
                                break;
                            }
                        case "set":
                            {
                                SetsOptions mapped = Map<SetsOptions>(options);
                                mapped.Logger = defaultLogger ? null : logger;
                                SetsResult result = Sdk.Set(Str(args[0])!, Str(args[1])!, mapped);
                                row["result"] = new JsonObject
                                {
                                    ["changed"] = new JsonArray([.. result.ChangedFilePaths.Select(path => (JsonNode?)JsonValue.Create(path))]),
                                    ["unchanged"] = new JsonArray([.. result.UnchangedFilePaths.Select(path => (JsonNode?)JsonValue.Create(path))])
                                };
                                break;
                            }
                        case "ls":
                            {
                                string[] files = Sdk.Ls(Str(args[0])!, args.Count > 1 && args[1] is not null ? [Str(args[1])!] : null,
                                    args.Count > 2 && args[2] is not null ? [Str(args[2])!] : null);
                                row["result"] = new JsonObject { ["files"] = new JsonArray([.. files.Select(path => (JsonNode?)JsonValue.Create(path))]) };
                                break;
                            }
                        case "events.config":
                            {
                                ConfigOptions mapped = Map<ConfigOptions>(options);
                                mapped.Logger = defaultLogger ? null : logger;
                                processEnv = mapped.ProcessEnv;
                                ConfigResult result = eventSdk.Config(mapped);
                                await_(eventSdk.FlushEvents());
                                row["result"] = new JsonObject { ["parsed"] = Dict(result.Parsed), ["error"] = DescribeError(result.Error) };
                                break;
                            }
                        case "events.get":
                            {
                                GetOptions mapped = Map<GetOptions>(options);
                                mapped.Logger = defaultLogger ? null : logger;
                                processEnv = mapped.ProcessEnv;
                                object? value = eventSdk.Get(Str(args[0]), mapped);
                                await_(eventSdk.FlushEvents());
                                JsonNode? shown = value switch
                                {
                                    null => new JsonObject { ["undefined"] = true },
                                    string text => text,
                                    IReadOnlyDictionary<string, string> dictionary => Dict(dictionary),
                                    IDictionary<string, string> dictionary => Dict(dictionary),
                                    _ => throw new InvalidOperationException(value.GetType().FullName)
                                };
                                row["result"] = new JsonObject { ["value"] = shown };
                                break;
                            }
                        case "events.set":
                            {
                                SetsOptions mapped = Map<SetsOptions>(options);
                                mapped.Logger = defaultLogger ? null : logger;
                                SetsResult result = eventSdk.Set(Str(args[0])!, Str(args[1])!, mapped);
                                await_(eventSdk.FlushEvents());
                                row["result"] = new JsonObject
                                {
                                    ["changed"] = new JsonArray([.. result.ChangedFilePaths.Select(path => (JsonNode?)JsonValue.Create(path))]),
                                    ["unchanged"] = new JsonArray([.. result.UnchangedFilePaths.Select(path => (JsonNode?)JsonValue.Create(path))])
                                };
                                break;
                            }
                        case "getColor":
                            row["result"] = new JsonObject
                            {
                                ["text"] = Sdk.GetColor(Str(args[0])!, Stream(args[1]))(Str(args[2])!)
                            };
                            break;
                        case "bold":
                            row["result"] = new JsonObject { ["text"] = Sdk.Bold(Str(args[0])!, Stream(args[1])) };
                            break;
                        case "write":
                            {
                                capturedOut.GetStringBuilder().Clear();
                                capturedError.GetStringBuilder().Clear();
                                TextWriter target = Str(args[0]) == "stdout" ? Console.Out : Console.Error;
                                foreach (JsonNode? text in args.Skip(1))
                                {
                                    target.Write(Str(text));
                                }

                                row["result"] = new JsonObject
                                {
                                    ["output"] = (Str(args[0]) == "stdout" ? capturedOut : capturedError).ToString()
                                };
                                break;
                            }
                        default:
                            throw new InvalidOperationException(fn);
                    }
                }
                catch (Exception error)
                {
                    row["threw"] = DescribeError(error);
                }

                if (options["processEnv"] is not null)
                {
                    row["processEnv"] = Dict(processEnv ?? new Dictionary<string, string>());
                }

                row["messages"] = messages;
                if (defaultLogger)
                {
                    row["stdout"] = capturedOut.ToString();
                    row["stderr"] = capturedError.ToString();
                }

                rows.Add(row);
            }

            return new JsonObject { ["calls"] = rows };
        }
        finally
        {
            RedactProcessOutput.Reset();
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Directory.SetCurrentDirectory(previousDirectory);
            foreach ((string name, string? value) in restore)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesNode(string name, Dictionary<string, string> files, Dictionary<string, string>? env, string callsJson)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        var callArray = JsonNode.Parse(callsJson)!.AsArray();
        string? vault = null;
        if (env is not null && env.ContainsKey("FAKE_CLIS"))
        {
            // the fake `op` / `bw` of test/fakes/secretcli.cs over a vault kept outside the work directory (the tree comparison)
            vault = WorkDir + "-fake";
            string bin = Path.Combine(vault, "bin");
            FakeCliInstaller.Install(bin, [.. env["FAKE_CLIS"].Split(',', StringSplitOptions.RemoveEmptyEntries)]);
            env = new Dictionary<string, string>(env)
            {
                ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                ["FAKE_SECRET_CLI_DIR"] = Path.Combine(vault, "data")
            };
            env.Remove("FAKE_CLIS");
        }

        // a logged-in Armor session (settings + device key in a config directory outside the work directory) against a fake API
        FakeArmorServer? armor = null;
        if (env is not null && env.TryGetValue("ARMOR", out string? armorMode))
        {
            armor = new FakeArmorServer();
            string config = WorkDir + "-armor";
            Directory.CreateDirectory(config);
            new ConfStore(config, ".device-key", "dotenvxpro dotenvxpro dotenvxpro")
                .Set("private_key/1", "6a2b0bbd6d3c3c6dd5ebc8d2ae0c4e4f8c1b3e0e2b3b1e5d9f7e1d3e9b5a4c21");
            File.WriteAllText(Path.Combine(config, ".env"),
                $"DOTENVX_ARMOR_HOSTNAME=\"{armor.BaseUrl}\"\nDOTENVX_ARMOR_USER=\"usr_1\"\nDOTENVX_ARMOR_USERNAME=\"alice\"\nDOTENVX_ARMOR_ON=\"true\"\nDOTENVX_VERSION_LAST_CHECK=\"99999999999999\"");
            string keyring = $"{{\"{FixedKeypairFixture.PublicKey}\":\"{FixedKeypairFixture.PrivateKey}\"}}";
            armor.Handler = request => (armorMode, request.Path) switch
            {
                ("keyring", "/api/armor/keyring") => new FakeResponse(200, keyring),
                ("empty", "/api/armor/keyring") => new FakeResponse(200, "{}"),
                ("error", "/api/armor/keyring") => new FakeResponse(500, "{\"error\":{\"code\":\"BOOM\",\"message\":\"down\"}}"),
                ("server", "/api/armor/keyring") => new FakeResponse(409,
                    "{\"error\":{\"code\":\"SERVER_SIDE_DECRYPTION_REQUIRED\",\"message\":\"decrypt on server\",\"meta\":{\"public_key\":\"" +
                    FixedKeypairFixture.PublicKey + "\",\"grant_token\":\"grantX\"}}}"),
                ("server", "/api/armor/decrypt") => new FakeResponse(200, "{\"src\":\"HELLO=\\\"from armor\\\"\\n\"}"),
                _ => new FakeResponse(404, "{}")
            };
            env = new Dictionary<string, string>(env) { ["DOTENVX_CONFIG"] = config };
            env.Remove("ARMOR");
        }

        string ArmorRequests() => armor is null
            ? ""
            : Regex.Replace(string.Join("\n", armor.RequestLog().Where(line => !line.Contains("/api/events"))), "[0-9a-f]{64,66}", "<hex>");

        string FreshVault()
        {
            armor?.Clear();
            if (vault is null)
            {
                return "";
            }

            string data = Path.Combine(vault, "data");
            if (Directory.Exists(data))
            {
                Directory.Delete(data, true);
            }

            Directory.CreateDirectory(Path.Combine(data, "op"));
            Directory.CreateDirectory(Path.Combine(data, "bw"));
            File.WriteAllText(Path.Combine(data, "op", "op___vault_item_field"), "op-secret");
            File.WriteAllText(Path.Combine(data, "bw", "github_password"), "bw-secret");
            File.WriteAllText(Path.Combine(data, "session"), "unlocked-session");
            File.WriteAllText(Path.Combine(data, "master"), "master-pw");

            return data;
        }

        string Calls() => vault is not null && File.Exists(Path.Combine(vault, "data", "calls.log"))
            ? File.ReadAllText(Path.Combine(vault, "data", "calls.log"))
            : "";

        string specFile = Path.Combine(Path.GetTempPath(), $"sdk-spec-{Guid.NewGuid():N}.json");
        var spec = new JsonObject { ["env"] = Dict(env ?? []), ["calls"] = callArray.DeepClone() };
        await File.WriteAllTextAsync(specFile, spec.ToJsonString());
        string oracle = Path.Combine(AppContext.BaseDirectory, "oracle", "sdk.cjs");
        try
        {
            Setup(files);
            FreshVault();
            CliResult node = await Fixture.RunNodeOracleAsync(WorkDir,
                new Dictionary<string, string> { ["DOTENVX_NO_NATIVE"] = "true" }, oracle, specFile,
                Path.Combine(Fixture.RepoRoot, "dotenvx"));
            node.ExitCode.ShouldBe(0, node.StandardError);
            JsonObject expected = JsonNode.Parse(node.StandardOutput.Trim().Split('\n')[^1])!.AsObject();
            string nodeTree = Snapshot();
            string nodeCalls = Calls() + ArmorRequests();

            Setup(files);
            FreshVault();
            JsonObject actual = RunPort(env, callArray);
            string portTree = Snapshot();
            string portCalls = Calls() + ArmorRequests();

            // `get` of every key lists the process environment. Node's child gets its variables in the order of a
            // ProcessStartInfo dictionary while the port reads the test host's own block, so on Unix (where that block is
            // not sorted, unlike Windows') only the order of that listing differs between the two sides: compare it sorted.
            string Show(JsonNode? value)
            {
                if (!OperatingSystem.IsWindows())
                {
                    foreach (JsonNode? row in value!["calls"]!.AsArray())
                    {
                        if (row?["fn"]?.GetValue<string>() == "get" && row["result"]?["value"] is JsonObject listing)
                        {
                            var sorted = listing.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                                .Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())).ToList();
                            listing.Clear();
                            foreach ((string key, JsonNode? item) in sorted)
                            {
                                listing[key] = item;
                            }
                        }
                    }
                }

                return Normalise(value!["calls"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }), WorkDir);
            }


            if (Environment.GetEnvironmentVariable("SDK_DUMP") is { Length: > 0 } dump)
            {
                string file = Regex.Replace(name, "[^A-Za-z0-9]+", "_");
                File.WriteAllText(Path.Combine(dump, file + ".node.json"), Show(expected));
                File.WriteAllText(Path.Combine(dump, file + ".port.json"), Show(actual));
            }

            Show(actual).ShouldBe(Show(expected), name);
            portTree.ShouldBe(nodeTree, name);
            portCalls.ShouldBe(nodeCalls, name);
        }
        finally
        {
            File.Delete(specFile);
            armor?.Dispose();
            if (vault is not null && Directory.Exists(vault))
            {
                try
                {
                    Directory.Delete(vault, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }
}
