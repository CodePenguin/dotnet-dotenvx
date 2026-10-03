using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using PrimitivesApi = Dotenvx.Primitives.Primitives;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - live-oracle sweep of the `@dotenvx/primitives` exports this port
/// implements (`scan`, `parseSync`, `upsert`, `sealed`, `encrypted`, `remove`, `publickeys`),
/// calling the REAL installed package from inside `dotenvx/` and comparing against the C#
/// counterparts in-process. Same shape as `ErrorMessageParityTest`.
/// </remarks>
public sealed class PrimitivesOracleParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private sealed record ParseCase(string Src, Dictionary<string, string>? Env = null, bool Overload = false);

    private static readonly string[] Sources = SourceTable.Split('|')
        .Select(c => c.Trim((char)10, (char)13, (char)32).Replace("{BOM}", ((char)0xFEFF).ToString()).Replace("{NBSP}", ((char)0xA0).ToString()).Replace("{LS}", ((char)0x2028).ToString())
            .Replace("¦", ((char)92).ToString()).Replace("⏎", ((char)10).ToString())
            .Replace("␍", ((char)13).ToString()))
        .ToArray();

    private const string SourceTable = """
        |
        A=1|
        A=${nullx}⏎B=${xnully}⏎C=${A-d}|
        A='x$$y'⏎B=${A}⏎C="$$ $& $¦`"⏎D=${Z:-$$}⏎F=${Z:-$'}|
        X=1⏎A=${X} ${X}⏎B=${Y:-d} ${Y:-d}|
        A=1⏎A=2|
        A=1 # comment⏎B="x # y" # c2|
        export A=1⏎export  B = 2|
        A: 1⏎B:2⏎C:  3|
        A="line1¦nline2"⏎B='raw¦nstay'⏎C=`tick¦nstay`|
        A="multi⏎line"⏎B=2|
        A='it¦'s'⏎B="say ¦"hi¦""⏎C=`a¦`b`|
        A=1␍⏎B=2␍C=3⏎|
        {BOM}A=1⏎B=2|
        HÉLLO=1⏎OK=2|
        A.B=1⏎A-B=2⏎.DOT=3|
        A=⏎B=''⏎C=""⏎D=   ⏎E=#c|
        A=  spaced  ⏎B="  kept  "|
        # only comment⏎⏎   ⏎A=1|
        A=1⏎B=${A}-x⏎C="$A y"⏎D='${A}'⏎E=¦${A}⏎F="¦$A"|
        A=${NOPE:-def}⏎B=${NOPE-def}⏎C=${A:+yes}⏎D=${A+yes}⏎E=${NOPE:+yes}⏎F=${NOPE+yes}|
        A=⏎B=${A:-def}⏎C=${A-def}⏎D=${A:+yes}⏎E=${A+yes}|
        A=1⏎A=${A}2⏎A=${A}3|
        A=${B}⏎B=2|
        A=${A}|
        A=${B:-${C:-deep}}|
        A=$B⏎B=1|
        A=${B:-a-b}⏎C=${B:-a:-b}⏎D=${B:-a+b}|
        A=$(echo hi)⏎B="$(echo there)"⏎C='$(echo no)'⏎D=`$(echo ticks)`|
        A=$(exit 1)⏎B=ok|
        A=${HOME}x⏎B=$USER_UNSET|
        A="x"y⏎B=z"w"|
        A=1 B=2⏎C=3|
        KEY=va=lue=more⏎URL=http://x.com/?a=b#frag|
        A="unterminated⏎B=2|
        A='unterminated⏎B=2|
        {NBSP}A={NBSP}1{NBSP}⏎B="x"{NBSP}# c|
        A=1{LS}B=2{LS}C="a{LS}b"|
        A=1␍B=2␍␍C=3|
        A=1⏎␍B=2⏎  ⏎C=3␍|
        export{NBSP}A=1⏎{NBSP}B=2|
        A=1 #c{LS}B=2|
        DOTENV_PUBLIC_KEY=abc⏎A=1
        """;

    private static readonly string[] Envs =
    [
        null!,
        "{\"A\":\"fromenv\"}",
        "{\"A\":\"\",\"B\":\"envB\"}",
        "{\"NOPE\":\"set\",\"HOME\":\"/h\"}"
    ];

    [Fact]
    public async Task ScanAndParseMatchRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        List<ParseCase> cases = [];
        foreach (string src in Sources)
        {
            foreach (string? env in Envs)
            {
                Dictionary<string, string>? dict = env == null
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(env);
                cases.Add(new ParseCase(src, dict));
                if (dict != null)
                {
                    cases.Add(new ParseCase(src, dict, true));
                }
            }
        }

        string json = JsonSerializer.Serialize(cases.Select(c => new
        {
            src = c.Src, env = c.Env ?? new Dictionary<string, string>(), overload = c.Overload
        }));
        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const cases = {{json}};
                          const out = cases.map(c => {
                            let scan, parse;
                            try { scan = p.scan(c.src, {}); } catch (e) { scan = { error: String(e.message) }; }
                            try {
                              const r = p.parseSync(c.src, { processEnv: { ...c.env }, overload: c.overload });
                              parse = { parsed: r.parsed, injected: r.injected, existed: r.existed, errors: (r.errors || []).length };
                            } catch (e) { parse = { error: String(e.message) }; }
                            return { scan, parse };
                          });
                          process.stdout.write(JSON.stringify(out));
                          """;

        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonArray node = JsonNode.Parse(result.StandardOutput)!.AsArray();
        node.Count.ShouldBe(cases.Count);

        List<string> mismatches = [];
        for (int i = 0; i < cases.Count; i++)
        {
            ParseCase c = cases[i];
            string label = $"src={JsonSerializer.Serialize(c.Src)} env={JsonSerializer.Serialize(c.Env)} overload={c.Overload}";

            // scan <-> Primitives.Parse (last wins / first wins)
            JsonObject? scanParsed = node[i]!["scan"]!["parsed"]?.AsObject();
            if (scanParsed != null)
            {
                Dictionary<string, object> last = PrimitivesApi.Parse(c.Src);
                Dictionary<string, object> first = PrimitivesApi.Parse(c.Src, collectAllValues: true);
                JsonObject expLast = new();
                JsonObject expFirst = new();
                foreach ((string k, JsonNode? v) in scanParsed)
                {
                    JsonArray arr = v!.AsArray();
                    expLast[k] = arr[^1]!.GetValue<string>();
                    expFirst[k] = arr[0]!.GetValue<string>();
                }

                Compare(mismatches, $"scan-last {label}", expLast, ToJson(last));
                Compare(mismatches, $"scan-first {label}", expFirst, ToJson(first));
            }

            // parseSync <-> Core Parse
            JsonNode? np = node[i]!["parse"];
            if (np!["error"] != null)
            {
                mismatches.Add($"node parseSync threw for {label}: {np["error"]}");
                continue;
            }

            Dictionary<string, string> env = c.Env == null ? new() : new Dictionary<string, string>(c.Env);
            ParseResult actual = new Parse(c.Src, null, env, c.Overload).Execute();
            JsonObject act = new()
            {
                ["parsed"] = ToJson(actual.Parsed),
                ["injected"] = ToJson(actual.Injected),
                ["existed"] = ToJson(actual.PreExisted)
            };
            JsonObject exp = new()
            {
                ["parsed"] = np["parsed"]!.DeepClone(),
                ["injected"] = np["injected"]!.DeepClone(),
                ["existed"] = np["existed"]!.DeepClone()
            };
            Compare(mismatches, $"parse {label}", exp, act);
            if (actual.Errors.Length != np["errors"]!.GetValue<int>())
            {
                mismatches.Add($"parse errors {label}: C# {actual.Errors.Length} != Node {np["errors"]}");
            }
        }

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(40)) + $"\n({mismatches.Count} total)");
    }

    private static readonly string[] UpsertKeys = ["A", "B", "DOTENV_PUBLIC_KEY", "NOPE"];

    private static readonly string[] UpsertValues =
        ["new", "with space", "", "a$$b", "$&", "it's", "x=y", "multi" + (char)10 + "line"];

    private static readonly string[] EncryptedValues =
        ["encrypted:abc", "encrypted:", "", "plain", " encrypted:x", "encrypted:abc" + (char)10, "ENCRYPTED:abc"];

    [Fact]
    public async Task UpsertRemoveSealedEncryptedPublicKeysMatchRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string json = JsonSerializer.Serialize(new { sources = Sources, keys = UpsertKeys, values = UpsertValues, enc = EncryptedValues });
        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const d = {{json}};
                          const t = (f) => { try { return { v: f() }; } catch (e) { return { err: true }; } };
                          const out = d.sources.map(src => ({
                            sealed: t(() => p.sealed(src)),
                            publickeys: t(() => p.publickeys(src)),
                            remove: d.keys.map(k => t(() => p.remove(src, k))),
                            upsert: d.keys.map(k => d.values.map(v => t(() => p.upsert(src, k, v))))
                          }));
                          const enc = d.enc.map(v => t(() => p.encrypted(v)));
                          process.stdout.write(JSON.stringify({ out, enc }));
                          """;

        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonNode root = JsonNode.Parse(result.StandardOutput)!;
        JsonArray node = root["out"]!.AsArray();

        List<string> mismatches = [];

        static JsonNode? Run<T>(Func<T> f)
        {
            try
            {
                return new JsonObject { ["v"] = JsonSerializer.SerializeToNode(f()) };
            }
            catch (Exception)
            {
                return new JsonObject { ["err"] = true };
            }
        }

        for (int i = 0; i < Sources.Length; i++)
        {
            string src = Sources[i];
            string label = $"src={JsonSerializer.Serialize(src)}";
            Compare(mismatches, $"sealed {label}", node[i]!["sealed"]!, Run(() => PrimitivesApi.Sealed(src))!);
            Compare(mismatches, $"publickeys {label}", node[i]!["publickeys"]!, Run(() => PrimitivesApi.PublicKeys(src))!);

            for (int k = 0; k < UpsertKeys.Length; k++)
            {
                string key = UpsertKeys[k];
                Compare(mismatches, $"remove {key} {label}", node[i]!["remove"]![k]!,
                    Run(() => PrimitivesApi.Remove(src, key))!);
                for (int v = 0; v < UpsertValues.Length; v++)
                {
                    string value = UpsertValues[v];
                    Compare(mismatches, $"upsert {key}={JsonSerializer.Serialize(value)} {label}",
                        node[i]!["upsert"]![k]![v]!, Run(() => PrimitivesApi.Upsert(src, key, value))!);
                }
            }
        }

        JsonArray enc = root["enc"]!.AsArray();
        for (int e = 0; e < EncryptedValues.Length; e++)
        {
            string value = EncryptedValues[e];
            Compare(mismatches, $"encrypted {JsonSerializer.Serialize(value)}", enc[e]!,
                Run(() => PrimitivesApi.Encrypted(value))!);
        }

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(40)) + $"\n({mismatches.Count} total)");
    }

    private static JsonObject ToJson<TValue>(IEnumerable<KeyValuePair<string, TValue>> d)
    {
        JsonObject o = new();
        foreach ((string k, TValue v) in d)
        {
            o[k] = v?.ToString();
        }

        return o;
    }

    private static void Compare(List<string> mismatches, string label, JsonNode expected, JsonNode actual)
    {
        if (!JsonNode.DeepEquals(expected, actual))
        {
            mismatches.Add($"{label}\n   Node: {expected.ToJsonString()}\n   C#:   {actual.ToJsonString()}");
        }
    }
}
