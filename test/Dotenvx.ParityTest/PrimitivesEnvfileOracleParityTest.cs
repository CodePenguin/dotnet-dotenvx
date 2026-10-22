using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Primitives;

using Shouldly;

using PrimitivesApi = Dotenvx.Primitives.Primitives;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - live-oracle checks of the @dotenvx/primitives 3.3.0 exports `policy`, `profile`, `check`, `redact`
/// and `injectsummary` (the Envfile model) against `Dotenvx.Primitives`: compiled rules, error messages and locations,
/// validators (url, email, ip), profile selection and redaction.
/// </remarks>
public sealed class PrimitivesEnvfileOracleParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static JsonNode? Run(Func<JsonNode?> f)
    {
        try
        {
            return f();
        }
        catch (MalformedEnvfileException e)
        {
            JsonObject error = new() { ["error"] = e.Code + "|" + e.Message };
            if (e.Location is { } location)
            {
                error["line"] = location.Line;
                error["column"] = location.Column;
                error["offset"] = location.Offset;
            }

            error["expected"] = e.Expected == null ? null : JsonValue.Create(e.Expected.Count);
            return error;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return new JsonObject { ["error"] = "TypeError|" + e.Message };
        }
    }

    private static JsonNode PolicyJson(EnvPolicy policy)
    {
        JsonObject keys = new();
        foreach ((string key, KeyPolicy rule) in policy.Keys)
        {
            JsonObject env = new() { ["type"] = rule.Env.Type };
            if (rule.Env.Min != null)
            {
                env["min"] = rule.Env.Min;
            }

            if (rule.Env.Max != null)
            {
                env["max"] = rule.Env.Max;
            }

            if (rule.Env.Enum != null)
            {
                env["enum"] = new JsonArray([.. rule.Env.Enum.Select(item => (JsonNode?)item)]);
            }

            env["required"] = rule.Env.Required;
            env["strict"] = rule.Env.Strict;
            keys[key] = new JsonObject
            {
                ["env"] = env,
                ["fs"] = new JsonObject { ["encrypted"] = rule.Encrypted },
                ["stdio"] = new JsonObject { ["redacted"] = rule.Redacted }
            };
        }

        return new JsonObject { ["keys"] = keys };
    }

    private static readonly string[] Sources =
    [
        "",
        "env \"A\"",
        "env 'A'\nenv \"B\", optional: true",
        "env \"A\", type: \"integer\", min: 1, max: 10",
        "env \"A\", type: \"port\"\nenv \"B\", type: \"port\", min: 1024",
        "env \"A\", type: \"string\", min: 2, max: 5, redacted: false, encrypted: false",
        "env \"A\", type: \"url\", enum: [\"https://a.example\", \"https://b.example\"]",
        "env \"A\", enum: [\"x\", 'y', 3, \"a\\\"b\", \"n\\n\"]",
        "env \"A\", type: \"integer\", enum: [1, 2, \"3\"]",
        "env \"A\", type: \"boolean\", enum: [\"true\", \"nope\"]",
        "env \"A\", type: \"email\"\nenv \"B\", type: \"ip\"",
        "strict true\nenv \"A\"",
        "strict false\nenv \"A\"\nstrict true",
        "env \"A\"\nenv \"A\"",
        "env \"A\", required: true, optional: true",
        "env \"A\", required: true, required: false",
        "env \"A\", redact: false, encrypt: false",
        "env \"A\", encypted: false",
        "env \"A\", min: 5, max: 1",
        "env \"A\", type: \"boolean\", min: 1",
        "env \"A\", type: \"string\", min: -1",
        "env \"A\", type: \"port\", min: -5, max: 70000",
        "env \"A\", type: \"nope\"",
        "env \"1A\"",
        "env A",
        "env \"A\" # comment\n# line comment\nenv \"B\"",
        "file \".env.production\" do\n  env \"A\", required: true\n  env \"B\"\nend\nenv \"A\", optional: true",
        "file \".env.production\" do\n  strict true\n  env \"A\"\nend\nfile \"./.env.production\" do\nend",
        "file \".env\" do\n  env \"A\"\n  env \"A\"\nend",
        "file \".env\" do\nenv \"A\"",
        "file \"a,b\" do\nend",
        "file \"\" do\nend",
        "profile \"development\", files: [\".env.local\", \".env\"]\nprofile \"production\", files: [\".env.production\"]",
        "profile \"development\", files: [\".env.local\", \".env\",]",
        "profile \"development\", files: []\nprofile \"development\", files: []",
        "profile \" x\", files: [\".env\"]",
        "profile \"x\", files: [\"*.env\"]",
        "profile \"x\", files: [\"a\", \"a\", \"./a\"]",
        "env \"A\",\n type: \"url\"",
        "env \"A\" , type : \"url\"",
        "env \"A\", type: 'url', enum: [ 'https://x.y' ,\n 'http://z' ]",
        "env \"A\"\r\nenv \"B\", type: \"integer\"\r\n",
        "\uFEFFenv \"A\"",
        "env",
        "env \"A\",",
        "env \"A\", type:",
        "env \"A\", type: \"integer\" env \"B\"",
        "frobnicate",
        "env \"A\", unknown: true",
        "env \"A\", required: yes",
        "env \"A\", enum: [",
        "env \"A\", enum: [1,",
        "env \"ä\"",
        "env \"A\"\n\u00e9",
        "strict",
        "strict maybe",
        "profile",
        "profile \"a\" files: []",
        "file \".env\" do end",
        "file \".env\" do\nenv \"A\"\nend extra"
    ];

    private static readonly string[] FileSelections = [".env", ".env.production", ".env.production,.env", "./.env.production", ".env,.env.local", ""];

    [Fact]
    public async Task PolicyMatchesRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const sources = {{JsonSerializer.Serialize(Sources)}};
                          const files = {{JsonSerializer.Serialize(FileSelections)}};
                          const out = [];
                          for (const src of sources) for (const f of files) {
                            try { out.push(p.policy(src, f)) }
                            catch (e) { out.push({ error: e.code + '|' + e.message, line: e.location?.start?.line, column: e.location?.start?.column, offset: e.location?.start?.offset, expected: e.cause?.expected ? e.cause.expected.length : null }) }
                          }
                          process.stdout.write(JSON.stringify(out));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonArray node = JsonNode.Parse(result.StandardOutput)!.AsArray();
        List<string> mismatches = [];
        int n = 0;
        foreach (string src in Sources)
        {
            foreach (string file in FileSelections)
            {
                JsonNode? expected = node[n++];
                JsonNode? actual = Run(() => PolicyJson(PrimitivesApi.Policy(src, file)));
                if (!JsonNode.DeepEquals(expected, actual))
                {
                    mismatches.Add($"policy {JsonSerializer.Serialize(src)} files={file}\n   Node: {expected?.ToJsonString()}\n   C#:   {actual?.ToJsonString()}");
                }
            }
        }

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(25)) + $"\n({mismatches.Count} total)");
    }

    [Fact]
    public async Task PolicyErrorsMatchRealPrimitivesOnMutatedSources()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] seeds =
        [
            "strict true\nenv \"DATABASE_URL\", type: \"url\"\nenv \"API_KEY\" # note\nenv 'PORT', type: 'port', min: 1024, optional: true\n",
            "profile \"development\", files: [\".env.local\", \".env\"]\nprofile \"production\", files: [\".env.production\"]\n",
            "file \".env.production\" do\n  strict false\n  env \"A\", required: true, enum: [\"x\", 'y', 3]\n  env \"B\", redacted: false, encrypted: false\nend\n",
            "env \"A\", type: \"string\", min: 1, max: 5\r\nenv \"B\", type: \"email\"\r\n"
        ];
        string alphabet = " \t\r\n,:\"'[]#-+0123456789abcdefghijklmnopqrstuvwxyz_AEZ\\é😀";
        Random random = new(20261011);
        List<string> sources = [];
        foreach (string seed in seeds)
        {
            for (int i = 0; i < 400; i++)
            {
                string text = seed;
                int edits = 1 + random.Next(2);
                for (int e = 0; e < edits; e++)
                {
                    int at = random.Next(text.Length + 1);
                    int kind = random.Next(3);
                    if (kind == 0 && at < text.Length)
                    {
                        text = text.Remove(at, 1);
                    }
                    else if (kind == 1)
                    {
                        string insert = alphabet[random.Next(alphabet.Length)].ToString();
                        if (char.IsHighSurrogate(insert[0]))
                        {
                            insert = "😀";
                        }
                        else if (char.IsLowSurrogate(insert[0]))
                        {
                            insert = "é";
                        }

                        text = text.Insert(at, insert);
                    }
                    else if (at < text.Length && !char.IsSurrogate(text[at]))
                    {
                        text = text.Remove(at, 1).Insert(at, alphabet[random.Next(alphabet.Length - 2)].ToString());
                    }
                }

                sources.Add(text);
            }
        }

        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const sources = {{JsonSerializer.Serialize(sources)}};
                          const out = sources.map(src => {
                            try { return p.policy(src, '.env.production,.env') }
                            catch (e) { return { error: e.code + '|' + e.message, line: e.location?.start?.line, column: e.location?.start?.column, offset: e.location?.start?.offset, expected: e.cause?.expected ? e.cause.expected.length : null } }
                          });
                          process.stdout.write(JSON.stringify(out));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonArray node = JsonNode.Parse(result.StandardOutput)!.AsArray();
        node.Count.ShouldBe(sources.Count);
        List<string> mismatches = [];
        int errors = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            string src = sources[i];
            JsonNode? actual = Run(() => PolicyJson(PrimitivesApi.Policy(src, ".env.production,.env")));
            if (node[i]!["error"] != null)
            {
                errors++;
            }

            if (!JsonNode.DeepEquals(node[i], actual))
            {
                mismatches.Add($"policy {JsonSerializer.Serialize(src)}\n   Node: {node[i]?.ToJsonString()}\n   C#:   {actual?.ToJsonString()}");
            }
        }

        errors.ShouldBeGreaterThan(sources.Count / 3, "the mutations should produce plenty of malformed sources");
        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(15)) + $"\n({mismatches.Count} of {sources.Count})");
    }

    private static readonly string[] ProfileEnvvars = ["", "production", "development", "nope", "  "];

    [Fact]
    public async Task ProfileMatchesRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] envelopes =
        [
            "profile \"development\", files: [\".env.local\", \".env\"]\nprofile \"production\", files: [\".env.production\", \"./.env.production\"]",
            "profile \"production\", files: [\".env.production\"]",
            "",
            "profile \"development\", files: []",
            "profile \"a\", files: [\"x\"]\nprofile \"a\", files: [\"y\"]"
        ];
        (string? Profile, string? F, Dictionary<string, string> Env)[] options =
        [
            (null, null, new()), ("production", null, new()), ("nope", null, new()), (" ", null, new()), ("", null, new()),
            (null, ".env.a,.env.b", new()), (null, ".env.a,,.env.b", new()), (null, "", new()), ("production", ".env.x", new()),
            (null, null, new() { ["NODE_ENV"] = "production" }), (null, null, new() { ["NODE_ENV"] = "nope" }),
            (null, null, new() { ["APP_ENV"] = "production", ["NODE_ENV"] = "development" }),
            (null, null, new() { ["DOTENV_ENV"] = "", ["NODE_ENV"] = "production" }),
            (null, null, new() { ["DOTENV_FILE"] = ".a,.b", ["NODE_ENV"] = "production" }),
            (null, null, new() { ["DOTENV_PATH"] = ".p" }), (null, null, new() { ["DOTENV_F"] = "./.env.production,.env.production" }),
            ("production", null, new() { ["DOTENV_FILE"] = ".a" }), (null, null, new() { ["DOTENV_FILE"] = "" })
        ];

        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const sources = {{JsonSerializer.Serialize(envelopes)}};
                          const options = {{JsonSerializer.Serialize(options.Select(o => new { profile = o.Profile, f = o.F, env = o.Env }))}};
                          const out = [];
                          for (const src of sources) for (const o of options) {
                            try { out.push({ v: p.profile(src, { profile: o.profile ?? undefined, f: o.f ?? undefined, processEnv: o.env }) }) }
                            catch (e) { out.push({ error: (e.code || 'TypeError') + '|' + e.message }) }
                          }
                          process.stdout.write(JSON.stringify(out));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonArray node = JsonNode.Parse(result.StandardOutput)!.AsArray();
        List<string> mismatches = [];
        int n = 0;
        foreach (string src in envelopes)
        {
            foreach ((string? profile, string? f, Dictionary<string, string> env) in options)
            {
                JsonNode? expected = node[n++];
                JsonNode? actual;
                try
                {
                    actual = new JsonObject
                    {
                        ["v"] = PrimitivesApi.Profile(src, new ProfileOptions { Profile = profile, F = f, ProcessEnv = env })
                    };
                }
                catch (MalformedEnvfileException e)
                {
                    actual = new JsonObject { ["error"] = e.Code + "|" + e.Message };
                }
                catch (ArgumentException e)
                {
                    actual = new JsonObject { ["error"] = "TypeError|" + e.Message };
                }

                if (!JsonNode.DeepEquals(expected, actual))
                {
                    mismatches.Add($"profile {JsonSerializer.Serialize(src)} p={profile} f={f} env={JsonSerializer.Serialize(env)}\n   Node: {expected?.ToJsonString()}\n   C#:   {actual?.ToJsonString()}");
                }
            }
        }

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(25)) + $"\n({mismatches.Count} total)");
    }

    private static readonly string[] CheckValues =
    [
        "", " ", "abc", "5", "-5", "+7", " 12 ", "0", "65535", "65536", "99999999999999999999", "1.5", "true", "false", "1", "0 ",
        "https://example.com", "http://", "not a url", "ftp://host:21/path", "postgres://u:p@h/db", "mailto:a@b.co", "http://a b",
        "http://[::1]:80/", "http://[::g]/", "http://1.2.3.4", "http://1.2.3.256", "http://0x7f.1", "http://exa mple.com",
        "file:///etc/passwd", "file://host/x", "C:\\dir", "//x", "a:b", "x:", "1http://a", "http://a:99999", "http://a:", "http://:80",
        "http://@a", "http://u@", "javascript:alert(1)", "data:text/plain,hi", "HTTPS://EXAMPLE.COM", "http://münchen.de", "http://a_b.com",
        "http://a%20b", "http://%41.com", "http://ex.com\\path", "ws://h", "tel:+1", "foo://", "foo://host:port", "foo://[::1", "https:example.com",
        "user@example.com", "a@b", "a@b.c", "a@b.co", "a b@c.de", "@a.de", "a@@b.de", "\"q\"@a.de", "a.b@c-d.de", "a@-b.de", "a@b-.de",
        "a@b.xn--p1ai", "a@b.123", "a..b@c.de", ".a@c.de", "ä@c.de", "a@ä.de", "127.0.0.1", "256.1.1.1", "01.1.1.1", "1.1.1", "::1", "::", "1::",
        "fe80::1%eth0", "fe80::1%", "::ffff:1.2.3.4", "1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:9", "12345::", "::1.2.3", "g::1", " ::1", "::1 ",
        "2001:db8::1", "[::1]", "1.2.3.4 "
    ];

    [Fact]
    public async Task CheckAndRedactMatchRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] types = ["string", "integer", "port", "boolean", "url", "email", "ip"];
        string[] secretsTexts = ["token=abc123", "abcabc", "a b a", "", "nothing here", "xyz xyz"];
        string[][] secretSets = [[], ["abc"], ["abc", "bc"], ["ab", "bc", "c"], [""], ["xyz", "x"], ["a", "b a"]];

        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const values = {{JsonSerializer.Serialize(CheckValues)}};
                          const types = {{JsonSerializer.Serialize(types)}};
                          const out = [];
                          for (const type of types) for (const v of values) {
                            const range = ['string', 'integer', 'port'].includes(type) ? ', min: 2, max: 9' : '';
                            const src = `env "K", type: "${type}"${range}\nenv "P", redacted: false\nenv "Q", optional: true`;
                            const policy = p.policy(src);
                            out.push(p.check(policy, { K: v, P: 'public', Z: 'other' }));
                            out.push(p.check(policy, { P: 'v' }));
                          }
                          const texts = {{JsonSerializer.Serialize(secretsTexts)}};
                          const sets = {{JsonSerializer.Serialize(secretSets)}};
                          const redacts = [];
                          for (const t of texts) for (const s of sets) redacts.push(p.redact(t, s));
                          const enumPolicy = p.policy('env "E", type: "integer", enum: [1, 2, 3]\nenv "F", enum: ["a", "b"]\nenv "G", type: "port", enum: [80, 443]');
                          const enums = [{ E: '2', F: 'a', G: '80' }, { E: ' 02 ', F: 'c', G: '8080' }, { E: '+3', F: '', G: ' 443 ' }].map(v => p.check(enumPolicy, v));
                          const numeric = p.check(p.policy('env "A"\nenv "B"'), { 10: 'x', 2: 'y', A: 'z', B: 'x' });
                          process.stdout.write(JSON.stringify({ out, redacts, enums, numeric }));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonNode node = JsonNode.Parse(result.StandardOutput)!;
        List<string> mismatches = [];

        static JsonNode CheckJson(CheckResult r)
        {
            JsonObject checkedValues = new();
            foreach ((string key, string value) in r.Checked)
            {
                checkedValues[key] = value;
            }

            return new JsonObject
            {
                ["checked"] = checkedValues,
                ["errors"] = new JsonArray([.. r.Errors.Select(e => (JsonNode?)new JsonObject
                {
                    ["key"] = e.Key,
                    ["code"] = e.Code,
                    ["rule"] = e.Rule,
                    ["message"] = e.Message,
                    ["strict"] = e.Strict
                })])
            };
        }

        int n = 0;
        foreach (string type in types)
        {
            EnvPolicy policy = PrimitivesApi.Policy(
                $"env \"K\", type: \"{type}\"{(type is "string" or "integer" or "port" ? ", min: 2, max: 9" : "")}\nenv \"P\", redacted: false\nenv \"Q\", optional: true");
            foreach (string v in CheckValues)
            {
                JsonNode? e1 = node["out"]![n++];
                JsonNode a1 = CheckJson(PrimitivesApi.Check(policy,
                    new Dictionary<string, string> { ["K"] = v, ["P"] = "public", ["Z"] = "other" }));
                JsonNode? e2 = node["out"]![n++];
                JsonNode a2 = CheckJson(PrimitivesApi.Check(policy, new Dictionary<string, string> { ["P"] = "v" }));
                if (!JsonNode.DeepEquals(e1, a1))
                {
                    mismatches.Add($"check type={type} K={JsonSerializer.Serialize(v)}\n   Node: {e1?.ToJsonString()}\n   C#:   {a1.ToJsonString()}");
                }

                if (!JsonNode.DeepEquals(e2, a2))
                {
                    mismatches.Add($"check missing type={type}\n   Node: {e2?.ToJsonString()}\n   C#:   {a2.ToJsonString()}");
                }
            }
        }

        n = 0;
        foreach (string text in secretsTexts)
        {
            foreach (string[] set in secretSets)
            {
                string expected = node["redacts"]![n++]!.GetValue<string>();
                string actual = PrimitivesApi.Redact(text, set);
                if (expected != actual)
                {
                    mismatches.Add($"redact {JsonSerializer.Serialize(text)} {JsonSerializer.Serialize(set)}: Node {expected} C# {actual}");
                }
            }
        }

        EnvPolicy enumPolicy = PrimitivesApi.Policy(
            "env \"E\", type: \"integer\", enum: [1, 2, 3]\nenv \"F\", enum: [\"a\", \"b\"]\nenv \"G\", type: \"port\", enum: [80, 443]");
        Dictionary<string, string>[] enumValues =
        [
            new() { ["E"] = "2", ["F"] = "a", ["G"] = "80" }, new() { ["E"] = " 02 ", ["F"] = "c", ["G"] = "8080" },
            new() { ["E"] = "+3", ["F"] = "", ["G"] = " 443 " }
        ];
        for (int i = 0; i < enumValues.Length; i++)
        {
            Compare(mismatches, $"enum {i}", node["enums"]![i], CheckJson(PrimitivesApi.Check(enumPolicy, enumValues[i])));
        }

        // integer-like keys come first, ascending (a JavaScript object's property order)
        Dictionary<string, string> numeric = new() { ["10"] = "x", ["2"] = "y", ["A"] = "z", ["B"] = "x" };
        Compare(mismatches, "numeric key order", node["numeric"],
            CheckJson(PrimitivesApi.Check(PrimitivesApi.Policy("env \"A\"\nenv \"B\""), numeric)));

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(40)) + $"\n({mismatches.Count} total)");
    }

    [Fact]
    public async Task ValidatorsMatchRealPrimitivesOnGeneratedValues()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] schemes = ["http", "https", "ftp", "file", "ws", "foo", "a+b", "HTTP", "", "1x", "x_y"];
        string[] separators = [":", "://", ":/", ":///", ":\\\\", "//", ":", ""];
        string[] hosts =
        [
            "example.com", "EXAMPLE.com", "a", "1.2.3.4", "1.2.3", "1.2.3.4.5", "0x7f.0.0.1", "0300.1", "4294967295", "4294967296", "[::1]",
            "[1:2:3:4:5:6:7:8]", "[::ffff:1.2.3.4]", "[1::2::3]", "[:1]", "[", "a b", "a_b", "a%41", "a%zz", "münchen.de", "xn--mnchen-3ya.de",
            "a..b", "a.b.", ".", "", "u@h", "u:p@h", "@", "u@", "h:80", "h:", "h:0", "h:65536", "h:abc", "h:8 0", "[::1]:80", "[::1]:", "-a-.b",
            "a#b", "a?b", "a/b", "a\\b", "a<b", "a|b", "a^b", "a\u0001b", "a\tb", "ａｂｃ", "faß.de", "a‍b"
        ];
        string[] tails = ["", "/", "/path?q#f", "?q", "#f", "/a b", "\\x"];
        List<string> values = [];
        foreach (string scheme in schemes)
        {
            foreach (string separator in separators)
            {
                foreach (string host in hosts)
                {
                    foreach (string tail in tails.Take(3))
                    {
                        values.Add(scheme + separator + host + tail);
                    }
                }
            }
        }

        foreach (string host in hosts)
        {
            foreach (string tail in tails)
            {
                values.Add("http://" + host + tail);
            }
        }

        values.AddRange(["  http://a.b  ", "http:/\t/a.b", "ht\ntp://a.b", "\u0001http://a.b", "http://a.b\u0001", " ", "http", ":", ":a"]);
        values = [.. values.Distinct()];

        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const policy = p.policy('env "K", type: "url"');
                          const values = {{JsonSerializer.Serialize(values)}};
                          const out = values.map(v => p.check(policy, { K: v }).errors.length === 0);
                          process.stdout.write(JSON.stringify(out));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonArray node = JsonNode.Parse(result.StandardOutput)!.AsArray();
        EnvPolicy policy = PrimitivesApi.Policy("env \"K\", type: \"url\"");
        List<string> mismatches = [];
        for (int i = 0; i < values.Count; i++)
        {
            bool actual = PrimitivesApi.Check(policy, new Dictionary<string, string> { ["K"] = values[i] }).Errors.Count == 0;
            if (node[i]!.GetValue<bool>() != actual)
            {
                mismatches.Add($"url {JsonSerializer.Serialize(values[i])}: Node {node[i]} C# {actual}");
            }
        }

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(40)) + $"\n({mismatches.Count} of {values.Count})");
    }

    private static void Compare(List<string> mismatches, string label, JsonNode? expected, JsonNode? actual)
    {
        if (!JsonNode.DeepEquals(expected, actual))
        {
            mismatches.Add($"{label}\n   Node: {expected?.ToJsonString()}\n   C#:   {actual?.ToJsonString()}");
        }
    }

    [Fact]
    public async Task InjectSummaryMatchesRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] pubs = ["031f20abcdef", "02aa", "03", "02deadbeefcafe0123"];
        string[] sources = ["armor", "native", "file", ".env.keys", "environment", "onepassword", "bitwarden", "macosKeychain", "custom", ""];
        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const pubs = {{JsonSerializer.Serialize(pubs)}};
                          const sources = {{JsonSerializer.Serialize(sources)}};
                          const out = [p.injectsummary(0), p.injectsummary(2, ['.env']), p.injectsummary(1, ['.env', '.env.local']),
                            p.injectsummary(3, [{ filepath: '.env', publicKeys: [], keySources: {} }]),
                            p.injectsummary(3, [{ filepath: '.env', publicKeys: [pubs[0]] }])];
                          for (const s of sources) {
                            out.push(p.injectsummary(3, [{ filepath: '.env', publicKeys: [pubs[0]], keySources: { [pubs[0]]: s } }]));
                            out.push(p.injectsummary(3, [{ filepath: 'a', publicKeys: [pubs[0], pubs[1]], keySources: { [pubs[0]]: s, [pubs[1]]: 'armor' } }, { filepath: 'b', publicKeys: [pubs[2], pubs[3]], keySources: { [pubs[2]]: 'file' } }, 'c']));
                          }
                          const srcs = p.injectsummary.sources(['x', { filepath: 'y', publicKeys: [pubs[3]], keySources: { [pubs[3]]: 'bitwarden' } }]);
                          process.stdout.write(JSON.stringify({ out, srcs }));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonNode node = JsonNode.Parse(result.StandardOutput)!;
        List<string> actual =
        [
            PrimitivesApi.InjectSummary(0), PrimitivesApi.InjectSummary(2, [new InjectSummaryFile(".env")]),
            PrimitivesApi.InjectSummary(1, [new InjectSummaryFile(".env"), new InjectSummaryFile(".env.local")]),
            PrimitivesApi.InjectSummary(3, [new InjectSummaryFile(".env", [], new Dictionary<string, string>())]),
            PrimitivesApi.InjectSummary(3, [new InjectSummaryFile(".env", [pubs[0]])])
        ];
        foreach (string s in sources)
        {
            actual.Add(PrimitivesApi.InjectSummary(3,
                [new InjectSummaryFile(".env", [pubs[0]], new Dictionary<string, string> { [pubs[0]] = s })]));
            actual.Add(PrimitivesApi.InjectSummary(3,
            [
                new InjectSummaryFile("a", [pubs[0], pubs[1]],
                    new Dictionary<string, string> { [pubs[0]] = s, [pubs[1]] = "armor" }),
                new InjectSummaryFile("b", [pubs[2], pubs[3]], new Dictionary<string, string> { [pubs[2]] = "file" }),
                new InjectSummaryFile("c")
            ]));
        }

        List<string> mismatches = [];
        for (int i = 0; i < actual.Count; i++)
        {
            string expected = node["out"]![i]!.GetValue<string>();
            if (expected != actual[i])
            {
                mismatches.Add($"injectsummary #{i}\n   Node: {expected}\n   C#:   {actual[i]}");
            }
        }

        InjectSummarySources sourceResult = PrimitivesApi.InjectSummarySourcesOf(
        [
            new InjectSummaryFile("x"),
            new InjectSummaryFile("y", [pubs[3]], new Dictionary<string, string> { [pubs[3]] = "bitwarden" })
        ]);
        Compare(mismatches, "sources", node["srcs"],
            new JsonObject
            {
                ["files"] = new JsonArray([.. sourceResult.Files.Select(f => (JsonNode?)f)]),
                ["suffix"] = sourceResult.Suffix
            });

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(25)));
    }
}
