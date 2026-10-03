using System.Text.Json;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using Shouldly;

using PrimitivesApi = Dotenvx.Primitives.Primitives;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - live-oracle checks of the `@dotenvx/primitives` exports `derive`, `decrypt`, `expand`,
/// `match`, `keyringSync`, `parseSync` and `parsearraysSync` against their C# counterparts in `Dotenvx.Primitives`.
/// </remarks>
public sealed class PrimitivesExportsOracleParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private sealed record EngineCase(string Src, Dictionary<string, string> Env, string[]? Ik, string[]? Ek, string[]? Fk, bool Overload);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly string Bs = ((char)92).ToString();

    private static JsonNode? Run<T>(Func<T> f)
    {
        try
        {
            return JsonSerializer.SerializeToNode(f());
        }
        catch (Exception e)
        {
            return JsonSerializer.SerializeToNode(new { error = e is DotenvxException d ? d.Code + "|" + d.Message : "other" });
        }
    }

    private static void Compare(List<string> mismatches, string label, JsonNode? expected, JsonNode? actual)
    {
        if (!JsonNode.DeepEquals(expected, actual))
        {
            mismatches.Add($"{label}\n   Node: {expected?.ToJsonString()}\n   C#:   {actual?.ToJsonString()}");
        }
    }

    private static JsonNode Dict<T>(IEnumerable<KeyValuePair<string, T>> d)
    {
        JsonObject o = new();
        foreach ((string k, T v) in d)
        {
            o[k] = JsonSerializer.SerializeToNode(v);
        }

        return o;
    }

    private static JsonNode ParseJson(PrimitivesParseOutput r)
    {
        return new JsonObject
        {
            ["parsed"] = Dict(r.Parsed), ["injected"] = Dict(r.Injected), ["existed"] = Dict(r.Existed),
            ["errors"] = JsonSerializer.SerializeToNode(r.Errors.Select(e => e.Code + "|" + e.Message))
        };
    }

    private static JsonNode ArraysJson(PrimitivesParseArraysOutput r)
    {
        return new JsonObject
        {
            ["parsed"] = Dict(r.Parsed), ["injected"] = Dict(r.Injected), ["existed"] = Dict(r.Existed),
            ["errors"] = JsonSerializer.SerializeToNode(r.Errors.Select(e => e.Code + "|" + e.Message))
        };
    }

    [Fact]
    public async Task DeriveDecryptExpandMatchMatchRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (string pub, string priv) = PrimitivesApi.Keypair();
        (string _, string priv2) = PrimitivesApi.Keypair();
        string enc = PrimitivesApi.Encrypt("hello", pub);
        string encBody = enc[PrimitivesApi.Prefix.Length..];

        object[][] decryptCases =
        [
            [priv, enc, true], [priv2, enc, true], ["", enc, true], [null!, enc, true], ["zz", enc, true],
            [priv, "plain", true], [priv, "encrypted:", true], [priv, "encrypted:AAAA", true],
            [priv, encBody, false], [priv, "plain", false], [priv, "encrypted:!!!", true]
        ];
        string[] derives = [priv, priv2, "zz", "", priv[..10]];
        string[] expandValues =
        [
            "plain", "${A}", "$A", "${A:-d}", "${B:-d}", "${E:-d}", "${E-d}", "${B-d}", "${A:+y}", "${B:+y}", "${E+y}",
            "${E:+y}", "${B+y}", "${X}${A}", "${A}${A}", "${A-x:-y}", "${A:-$&}", "${B:-$$}", "${B:-a+b}", "${nullx}",
            Bs + "${A}", "${A}" + Bs + "$", "${B:-${A}}", "${LIT}", "${C}"
        ];
        string[][] matchGlobs = [["*"], ["A*"], ["*B"], ["A"], [".*"], ["**"], ["a?c"], ["[ab]c"], ["{a,b}"], ["!A"]];
        string[] matchInputs = ["A", "AB", "B", ".A", "abc", "abd", "ac", "bc", "a", "b", "x/y", ".env", "A.B"];
        string[][] matchIgnores = [[], ["A"], ["A*"], ["*"], [".*"]];

        string script = $$"""
                          const p = require('@dotenvx/primitives');
                          const t = f => { try { return f(); } catch (e) { return { error: (e.code || 'x') + '|' + e.message }; } };
                          const dec = {{JsonSerializer.Serialize(decryptCases)}}.map(([k, v, pre]) =>
                              t(() => p.decrypt(k === null ? undefined : k, v, { prefix: pre })));
                          const der = {{JsonSerializer.Serialize(derives)}}.map(k => t(() => p.derive(k)));
                          const env = { A: 'av', E: '', LIT: 'x' };
                          const exp = {{JsonSerializer.Serialize(expandValues)}}.flatMap(v => [false, true].map(o =>
                              t(() => p.expand(v, { overload: o, processEnv: env, runningParsed: { C: 'cv' }, literals: { C: '$A', LIT: 'noexpand' } }))));
                          const mat = [];
                          for (const g of {{JsonSerializer.Serialize(matchGlobs)}})
                            for (const ig of {{JsonSerializer.Serialize(matchIgnores)}}) {
                              const m = p.match(g, { ignore: ig });
                              mat.push({{JsonSerializer.Serialize(matchInputs)}}.map(i => m(i)));
                            }
                          process.stdout.write(JSON.stringify({ dec, der, exp, mat }));
                          """;
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
        JsonNode node = JsonNode.Parse(result.StandardOutput)!;
        List<string> mismatches = [];

        for (int i = 0; i < decryptCases.Length; i++)
        {
            object[] c = decryptCases[i];
            Compare(mismatches, $"decrypt {i}", node["dec"]![i],
                Run(() => PrimitivesApi.Decrypt((string?)c[0], (string)c[1], (bool)c[2])));
        }

        for (int i = 0; i < derives.Length; i++)
        {
            string d = derives[i];
            JsonNode? exp = node["der"]![i];
            JsonNode? act = Run(() => PrimitivesApi.Derive(d));
            // only whether it throws matters for invalid keys (messages come from different crypto libraries)
            if (exp is JsonObject)
            {
                (act is JsonObject).ShouldBeTrue($"derive {d} should throw");
            }
            else
            {
                Compare(mismatches, $"derive {d}", exp, act);
            }
        }

        Dictionary<string, string> env = new() { ["A"] = "av", ["E"] = "" };
        int n = 0;
        foreach (string v in expandValues)
        {
            foreach (bool overload in new[] { false, true })
            {
                string value = v;
                Compare(mismatches, $"expand {v} overload={overload}", node["exp"]![n++],
                    Run(() => PrimitivesApi.Expand(value, overload, new Dictionary<string, string>(env) { ["LIT"] = "x" },
                        new Dictionary<string, string> { ["C"] = "cv" },
                        new Dictionary<string, string> { ["C"] = "$A", ["LIT"] = "noexpand" })));
            }
        }

        int row = 0;
        foreach (string[] g in matchGlobs)
        {
            foreach (string[] ig in matchIgnores)
            {
                Func<string, bool> m = PrimitivesApi.Match(g, ig);
                Compare(mismatches, $"match {string.Join(",", g)} ignore {string.Join(",", ig)}", node["mat"]![row++],
                    JsonSerializer.SerializeToNode(matchInputs.Select(m)));
            }
        }

        mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(40)) + $"\n({mismatches.Count} total)");
    }

    [Fact]
    public async Task KeyringAndParseEnginesMatchRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (string pub, string priv) = PrimitivesApi.Keypair();
        (string _, string priv2) = PrimitivesApi.Keypair();
        string keysFile = Path.Combine(Path.GetTempPath(), $"keyring-{Guid.NewGuid():N}.keys");
        File.WriteAllText(keysFile, $"DOTENV_PRIVATE_KEY=\"{priv}\"\nOTHER=1\nDOTENV_PRIVATE_KEY_X=\"{priv2},bad\"\n");
        string missing = Path.Combine(Path.GetTempPath(), "does-not-exist.keys");
        try
        {
            string e1 = PrimitivesApi.Encrypt("hello", pub);
            string e2 = PrimitivesApi.Encrypt("world", pub);
            string[] sources =
            [
                "A=1",
                $"DOTENV_PUBLIC_KEY=\"{pub}\"\nA=\"{e1}\"\nB=plain\nC=${{A}}!",
                $"DOTENV_PUBLIC_KEY=\"{pub}\"\nA=\"{e1}\"\nA=\"{e2}\"\nB=${{A}}",
                $"DOTENV_PUBLIC_KEY=\"{pub}\"\nA=\"{e1}\"\nB=\"{e2}\"\nC=3",
                $"A=\"{e1}\"\nB=$(echo hi)\nC='$(echo no)'",
                $"DOTENV_PUBLIC_KEY=\"{pub}\"\nA=\"{e1}\"\nB=\"$(echo {{A}})\"",
                $"DOTENV_PUBLIC_KEY=\"{pub}\"\nA=1\nA=2\nB=${{A}}\nC=${{Z:-fb}}"
            ];
            Dictionary<string, string>[] envs =
            [
                new(),
                new() { ["DOTENV_PRIVATE_KEY"] = priv },
                new() { ["DOTENV_PRIVATE_KEY"] = priv2 },
                new() { ["DOTENV_PRIVATE_KEY_X"] = priv },
                new() { ["DOTENV_PUBLIC_KEY"] = pub },
                new() { ["DOTENV_PRIVATE_KEY"] = priv, ["A"] = "preset" },
                new() { ["DOTENV_PRIVATE_KEY"] = priv, ["A"] = e1 },
                new() { ["DOTENV_PRIVATE_KEY"] = priv, ["Z"] = "" }
            ];
            (string[]? Ik, string[]? Ek)[] filters = [(null, null), (["A"], null), (null, ["A"]), (["*"], ["B"])];
            string[]?[] fks = [null, [keysFile], [missing, keysFile]];

            List<EngineCase> cases = [];
            foreach (string src in sources)
            {
                foreach (Dictionary<string, string> env in envs)
                {
                    foreach ((string[]? ik, string[]? ek) in filters)
                    {
                        foreach (string[]? fk in fks)
                        {
                            foreach (bool overload in new[] { false, true })
                            {
                                cases.Add(new EngineCase(src, env, ik, ek, fk, overload));
                            }
                        }
                    }
                }
            }

            string json = JsonSerializer.Serialize(cases, Web);
            string script = $$"""
                              const p = require('@dotenvx/primitives');
                              const cases = {{json}};
                              const fmt = r => ({ parsed: r.parsed, injected: r.injected, existed: r.existed,
                                  errors: (r.errors || []).map(e => e.code + '|' + e.message) });
                              const out = cases.map(c => {
                                const o = { processEnv: { ...c.env }, overload: c.overload, ik: c.ik || undefined, ek: c.ek || undefined, fk: c.fk || undefined };
                                return { sync: fmt(p.parseSync(c.src, o)), arrays: fmt(p.parsearraysSync(c.src, { ...o, processEnv: { ...c.env } })) };
                              });
                              const rings = [];
                              for (const fk of [undefined, {{JsonSerializer.Serialize(keysFile)}}, [{{JsonSerializer.Serialize(missing)}}, {{JsonSerializer.Serialize(keysFile)}}]])
                                for (const env of {{JsonSerializer.Serialize(envs)}}) {
                                  const provider = k => (k === {{JsonSerializer.Serialize(pub)}} ? { [k]: {{JsonSerializer.Serialize(priv2)}} } : {});
                                  rings.push([p.keyringSync({ fk, processEnv: { ...env } }), p.keyringSync({ fk, processEnv: { ...env }, ring: { [{{JsonSerializer.Serialize(pub)}}]: '' }, provider })]);
                                }
                              process.stdout.write(JSON.stringify({ out, rings }));
                              """;
            CliResult result = await Fixture.RunNodeScriptAsync(script);
            result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
            JsonNode node = JsonNode.Parse(result.StandardOutput)!;
            List<string> mismatches = [];

            for (int i = 0; i < cases.Count; i++)
            {
                EngineCase c = cases[i];
                PrimitivesParseOptions Options() => new()
                {
                    Overload = c.Overload, Ik = c.Ik, Ek = c.Ek, Fk = c.Fk,
                    ProcessEnv = new Dictionary<string, string>(c.Env)
                };

                string label = $"src={JsonSerializer.Serialize(c.Src)} env={JsonSerializer.Serialize(c.Env)} " +
                               $"ik={JsonSerializer.Serialize(c.Ik)} ek={JsonSerializer.Serialize(c.Ek)} " +
                               $"fk={JsonSerializer.Serialize(c.Fk)} overload={c.Overload}";
                Compare(mismatches, $"parseSync {label}", node["out"]![i]!["sync"],
                    ParseJson(PrimitivesApi.ParseSync(c.Src, Options())));
                Compare(mismatches, $"parsearraysSync {label}", node["out"]![i]!["arrays"],
                    ArraysJson(PrimitivesApi.ParseArraysSync(c.Src, Options())));
            }

            int r = 0;
            foreach (string[]? fk in fks)
            {
                foreach (Dictionary<string, string> env in envs)
                {
                    Dictionary<string, string> plain = PrimitivesApi.KeyringSync(new KeyringOptions
                    {
                        Fk = fk, ProcessEnv = new Dictionary<string, string>(env)
                    });
                    Dictionary<string, string> provided = PrimitivesApi.KeyringSync(new KeyringOptions
                    {
                        Fk = fk,
                        ProcessEnv = new Dictionary<string, string>(env),
                        Ring = new Dictionary<string, string> { [pub] = "" },
                        Provider = k => k == pub ? new Dictionary<string, string> { [k] = priv2 } : null
                    });
                    Compare(mismatches, $"keyringSync fk={JsonSerializer.Serialize(fk)} env={JsonSerializer.Serialize(env)}",
                        node["rings"]![r]![0], Dict(plain));
                    Compare(mismatches, $"keyringSync+provider fk={JsonSerializer.Serialize(fk)} env={JsonSerializer.Serialize(env)}",
                        node["rings"]![r]![1], Dict(provided));
                    r++;
                }
            }

            mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(30)) + $"\n({mismatches.Count} total)");
        }
        finally
        {
            File.Delete(keysFile);
        }
    }

    [Fact]
    public async Task ReadmeExamplesScanOptionsUpsertArraysAndKeyringFkMatchRealPrimitives()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string nl = ((char)10).ToString();
        string[] scanSources =
        [
            "HELLO=first" + nl + "HELLO=second # optional", "HELLO=World", "A=1 #c" + nl + "B=\"x\" # d e" + nl + "C=3#nocomment",
            "A=\"l1" + Bs + "nl2" + Bs + "tt\"" + nl + "B=2", "A=1" + (char)13 + (char)10 + "B=2", "# only" + nl + "HELLO=1 #" + nl + "X=2"
        ];
        (string[]? Ik, string[]? Ek, bool Expand, bool Convert, bool Transform)[] scanOptions =
        [
            (null, null, true, true, false), (["HELLO"], null, true, true, false), (null, ["HELLO"], true, true, false),
            (["*"], ["B"], true, true, false), (null, null, false, true, false), (null, null, true, false, false),
            (null, null, true, true, true)
        ];
        (string Src, string Key, string[]? Array, string? Single)[] upserts =
        [
            ("HELLO=one" + nl + "HELLO=two", "HELLO", ["uno", "dos"], null), ("HELLO=one" + nl + "HELLO=two", "HELLO", ["x"], null),
            ("HELLO=one" + nl + "HELLO=two", "HELLO", ["a", "b", "c"], null), ("HELLO=one" + nl + "HELLO=two", "HELLO", [], null),
            ("A=1", "HELLO", ["a", "b"], null), ("", "HELLO", ["a"], null), ("HELLO=one" + nl + "HELLO=two", "HELLO", null, "s")
        ];

        (string pub, string priv) = PrimitivesApi.Keypair();
        string keysFile = Path.Combine(Path.GetTempPath(), $"readme-{Guid.NewGuid():N}.keys");
        File.WriteAllText(keysFile, $"DOTENV_PRIVATE_KEY=\"{priv}\"\n");
        try
        {
            string script = $$"""
                              const p = require('@dotenvx/primitives');
                              const t = f => { try { return f(); } catch (e) { return { error: (e.code || 'x') + '|' + e.message }; } };
                              const nl = String.fromCharCode(10);
                              const scans = [];
                              for (const src of {{JsonSerializer.Serialize(scanSources)}})
                                for (const [ik, ek, ex, cv, tr] of {{JsonSerializer.Serialize(scanOptions.Select(o => new object?[] { o.Ik, o.Ek, o.Expand, o.Convert, o.Transform }))}}) {
                                  const o = { ik: ik || undefined, ek: ek || undefined, expandDoubleQuotedNewlines: ex, convertWindowsNewlines: cv };
                                  scans.push(tr ? p.scan(src, o, ({ value }) => value.toUpperCase()) : p.scan(src, o));
                                }
                              const ups = {{JsonSerializer.Serialize(upserts.Select(u => new object?[] { u.Src, u.Key, u.Array, u.Single }))}}
                                .map(([s, k, a, one]) => t(() => p.upsert(s, k, a === null ? one : a)));
                              const fk = {{JsonSerializer.Serialize(keysFile)}};
                              const rings = [[], fk, [fk], undefined].map(f => p.keyringSync({ fk: f, processEnv: {} }));
                              const allowed = p.match(['DOTENV_*'], { ignore: ['DOTENV_PRIVATE_*'] });
                              const readme = {
                                expand: p.expand('postgres://${USER}:$PASS@localhost', { processEnv: { USER: 'scott', PASS: 'secret' } }),
                                match: [allowed('DOTENV_PUBLIC_KEY'), allowed('DOTENV_PRIVATE_KEY')],
                                publickeys: p.publickeys('DOTENV_PUBLIC_KEY=public-a' + nl + 'DOTENV_PUBLIC_KEY_PRODUCTION=public-b'),
                                sealed: [p.sealed('HELLO=encrypted:abc123'), p.sealed('HELLO=World'), p.sealed('')],
                                remove: p.remove('HELLO=one' + nl + 'FOO=bar' + nl + 'HELLO=two' + nl, 'HELLO'),
                                parse: p.parseSync('A=file' + nl + 'B=$A' + nl + 'HELLO=World', { processEnv: {}, fk: [] }).parsed,
                                arrays: p.parsearraysSync('HELLO=World' + nl + 'HELLO=Node', { processEnv: {}, fk: [] }).parsed
                              };
                              process.stdout.write(JSON.stringify({ scans, ups, rings, readme }));
                              """;
            CliResult result = await Fixture.RunNodeScriptAsync(script);
            result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");
            JsonNode node = JsonNode.Parse(result.StandardOutput)!;
            List<string> mismatches = [];

            int n = 0;
            foreach (string src in scanSources)
            {
                foreach ((string[]? ik, string[]? ek, bool ex, bool cv, bool tr) in scanOptions)
                {
                    ScanResult r = PrimitivesApi.Scan(src,
                        new ScanOptions { Ik = ik, Ek = ek, ExpandDoubleQuotedNewlines = ex, ConvertWindowsNewlines = cv },
                        tr ? e => e.Value.ToUpperInvariant() : null);
                    JsonNode actual = new JsonObject { ["parsed"] = Dict(r.Parsed), ["comments"] = Dict(r.Comments) };
                    Compare(mismatches, $"scan {JsonSerializer.Serialize(src)} ik={JsonSerializer.Serialize(ik)} ek={JsonSerializer.Serialize(ek)} ex={ex} cv={cv} tr={tr}",
                        node["scans"]![n++], actual);
                }
            }

            for (int i = 0; i < upserts.Length; i++)
            {
                (string src, string key, string[]? array, string? single) = upserts[i];
                Compare(mismatches, $"upsert {i}", node["ups"]![i],
                    Run(() => array is null ? PrimitivesApi.Upsert(src, key, single!) : PrimitivesApi.Upsert(src, key, array)));
            }

            // fk: [] (no files), "file", [file], undefined (default .env.keys, absent here)
            string[]?[] fks = [[], [keysFile], [keysFile], null];
            for (int i = 0; i < 4; i++)
            {
                Compare(mismatches, $"keyring fk #{i}", node["rings"]![i],
                    Dict(PrimitivesApi.KeyringSync(new KeyringOptions { Fk = fks[i], ProcessEnv = new Dictionary<string, string>() })));
            }

            JsonNode readme = node["readme"]!;
            Dictionary<string, string> userPass = new() { ["USER"] = "scott", ["PASS"] = "secret" };
            Compare(mismatches, "readme expand", readme["expand"],
                JsonValue.Create(PrimitivesApi.Expand("postgres://${USER}:$PASS@localhost", false, userPass,
                    new Dictionary<string, string>(), new Dictionary<string, string>())));
            Func<string, bool> allowed = PrimitivesApi.Match(["DOTENV_*"], ["DOTENV_PRIVATE_*"]);
            Compare(mismatches, "readme match", readme["match"],
                JsonSerializer.SerializeToNode(new[] { allowed("DOTENV_PUBLIC_KEY"), allowed("DOTENV_PRIVATE_KEY") }));
            Compare(mismatches, "readme publickeys", readme["publickeys"],
                JsonSerializer.SerializeToNode(PrimitivesApi.PublicKeys("DOTENV_PUBLIC_KEY=public-a" + nl + "DOTENV_PUBLIC_KEY_PRODUCTION=public-b")));
            Compare(mismatches, "readme sealed", readme["sealed"],
                JsonSerializer.SerializeToNode(new[]
                {
                    PrimitivesApi.Sealed("HELLO=encrypted:abc123"), PrimitivesApi.Sealed("HELLO=World"), PrimitivesApi.Sealed("")
                }));
            Compare(mismatches, "readme remove", readme["remove"],
                JsonValue.Create(PrimitivesApi.Remove("HELLO=one" + nl + "FOO=bar" + nl + "HELLO=two" + nl, "HELLO")));
            Compare(mismatches, "readme parse", readme["parse"],
                Dict(PrimitivesApi.ParseSync("A=file" + nl + "B=$A" + nl + "HELLO=World",
                    new PrimitivesParseOptions { ProcessEnv = new Dictionary<string, string>(), Fk = [] }).Parsed));
            Compare(mismatches, "readme parsearrays", readme["arrays"],
                Dict(PrimitivesApi.ParseArraysSync("HELLO=World" + nl + "HELLO=Node",
                    new PrimitivesParseOptions { ProcessEnv = new Dictionary<string, string>(), Fk = [] }).Parsed));

            mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Take(30)) + $"\n({mismatches.Count} total)");
        }
        finally
        {
            File.Delete(keysFile);
        }
    }
}
