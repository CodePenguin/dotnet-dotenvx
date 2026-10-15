using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - the interactive flows (protect, spec, set, custody, feedback) need a terminal on stdin and stderr, so they
/// run in a pseudo terminal (test/tools/pty). Node and the .NET CLI get the same keystrokes from the same starting files; the
/// screen after every step (text and cell attributes, read through a terminal emulator), the exit code and the resulting files
/// must be identical.
/// </remarks>
[Collection(NodeParityCollection.Name)]
public class PtyParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string Down = "\u001b[B";

    private const string Up = "\u001b[A";

    private const string Enter = "\r";

    private const string CtrlC = "\u0003";

    private const string Esc = "\u001b";

    private const string Left = "\u001b[D";

    /// <summary>When set, `op`/`bw` are the fake of test/fakes/secretcli.cs on PATH over a vault holding these files (name, content).</summary>
    private Dictionary<string, string>? _vault;

    /// <summary>When set, a logged-in Armor session against this fake API (settings, device key, token in the fake OS secret store).</summary>
    private FakeArmorServer? _armorServer;

    private string _armorRequests = "";

    private sealed record Step(string? Expect, string? Send, int SettleMs = 150);

    private static Step Wait(string expect, string send)
    {
        return new Step(expect, send);
    }

    private static Step Send(string send)
    {
        return new Step(null, send);
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

    private void Git(Dictionary<string, string> env, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = WorkDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach ((string key, string value) in env)
        {
            info.Environment[key] = value;
        }

        using Process process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
    }

    private string Snapshot()
    {
        var lines = new List<string>();
        foreach (string file in Directory.EnumerateFiles(WorkDir, "*", SearchOption.AllDirectories).Order())
        {
            string relative = Path.GetRelativePath(WorkDir, file).Replace('\\', '/');
            if (relative.StartsWith(".git/", StringComparison.Ordinal) && !relative.StartsWith(".git/hooks", StringComparison.Ordinal))
            {
                if (relative != ".git/config" && relative != ".git/info/attributes")
                {
                    continue;
                }
            }

            string content = File.ReadAllText(file);
            if (relative == ".git/config")
            {
                content = string.Join("\n", content.Split('\n').Where(line => line.Contains("dotenvx", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith('[')));
            }

            // generated key pairs and the ciphertexts made from them differ on every run
            content = System.Text.RegularExpressions.Regex.Replace(content, "(PUBLIC_KEY[A-Za-z_]*=\")[0-9a-f]+\"", "$1<public>\"");
            content = System.Text.RegularExpressions.Regex.Replace(content, "(PRIVATE_KEY[A-Za-z_]*=)[0-9a-f]+", "$1<private>");
            content = System.Text.RegularExpressions.Regex.Replace(content, "encrypted:[A-Za-z0-9+/=]+", "encrypted:<cipher>");

            lines.Add($"--- {relative}\n{content}");
        }

        return string.Join("\n", lines);
    }

    private async Task<JsonObject?> Pty(bool node, Dictionary<string, string> env, IEnumerable<Step> steps, string[] args)
    {
        string[] command = node
            ? [Fixture.NodePath, Fixture.DotenvxJsPath, .. args]
            : [Fixture.DotnetPath, Fixture.DotenvxDllPath, .. args];
        var spec = new JsonObject
        {
            ["command"] = new JsonArray([.. command.Select(part => (JsonNode)part)]),
            ["cwd"] = WorkDir,
            ["env"] = JsonSerializer.SerializeToNode(env),
            ["cols"] = 80,
            ["rows"] = 24,
            ["steps"] = new JsonArray([.. steps.Select(step => (JsonNode)new JsonObject
            {
                ["expect"] = step.Expect, ["send"] = step.Send, ["settleMs"] = step.SettleMs
            })]),
            ["exitTimeoutMs"] = 20000
        };
        string specFile = Path.Combine(Path.GetTempPath(), $"pty-spec-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(specFile, spec.ToJsonString());
        try
        {
            CliResult? result = await Fixture.RunPtyAsync(specFile);
            if (result is null)
            {
                return null;
            }

            result.ExitCode.ShouldBe(0, result.StandardError);

            return JsonNode.Parse(result.StandardOutput.Trim().Split('\n')[^1])!.AsObject();
        }
        finally
        {
            File.Delete(specFile);
        }
    }

    /// <summary>Node's screens, then the port's, from the same starting files; <paramref name="prepare" /> runs on Node first (state the scenario builds on).</summary>
    private async Task Compare(Dictionary<string, string> files, bool git, IEnumerable<Step>? prepare, string[]? prepareArgs,
        IEnumerable<Step> steps, params string[] args)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string home = Path.Combine(Path.GetTempPath(), "dotenvx-pty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var env = new Dictionary<string, string>
            {
                ["GIT_CONFIG_GLOBAL"] = Path.Combine(home, "gitconfig"),
                ["GIT_CONFIG_SYSTEM"] = Path.Combine(home, "gitconfig-system"),
                ["XDG_CONFIG_HOME"] = Path.Combine(home, "xdg"),
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["DOTENVX_CONFIG"] = Path.Combine(home, "dotenvx"),
                ["DOTENVX_NO_NATIVE"] = "true",
                ["CI"] = string.Empty,
                ["NO_COLOR"] = string.Empty
            };

            async Task<(JsonObject? Screens, string Tree)> Side(bool node)
            {
                foreach (string entry in Directory.GetFileSystemEntries(WorkDir))
                {
                    if (Directory.Exists(entry))
                    {
                        ForceDelete(entry);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }

                if (Directory.Exists(home))
                {
                    foreach (string entry in Directory.GetFileSystemEntries(home))
                    {
                        if (Directory.Exists(entry))
                        {
                            ForceDelete(entry);
                        }
                        else
                        {
                            File.Delete(entry);
                        }
                    }
                }

                foreach ((string name, string content) in files)
                {
                    WriteFile(name, content);
                }

                if (_armorServer is not null)
                {
                    string config = env["DOTENVX_CONFIG"];
                    Directory.CreateDirectory(config);
                    new global::Dotenvx.Interop.DotenvxJs.Helpers.ConfStore(config, ".device-key", "dotenvxpro dotenvxpro dotenvxpro")
                        .Set("private_key/1", "6a2b0bbd6d3c3c6dd5ebc8d2ae0c4e4f8c1b3e0e2b3b1e5d9f7e1d3e9b5a4c21");
                    File.WriteAllText(Path.Combine(config, ".env"),
                        $"DOTENVX_ARMOR_HOSTNAME=\"{_armorServer.BaseUrl}\"\nDOTENVX_ARMOR_USER=\"usr_1\"\nDOTENVX_ARMOR_USERNAME=\"alice\"\n" +
                        "DOTENVX_ARMOR_ON=\"true\"\nDOTENVX_VERSION_LAST_CHECK=\"99999999999999\"");
                    string armorVault = Path.Combine(home, "armorvault");
                    Directory.CreateDirectory(Path.Combine(armorVault, "native"));
                    File.WriteAllText(Path.Combine(armorVault, "native", "dotenvx_DOTENVX_ARMOR_TOKEN"), "tok_abc1234567890");
                    FakeCliInstaller.Install(Path.Combine(home, "armorbin"), OperatingSystem.IsWindows() ? "powershell" : "secret-tool");
                    env["PATH"] = Path.Combine(home, "armorbin") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
                    env["FAKE_SECRET_CLI_DIR"] = armorVault;
                    env["DOTENVX_NO_NATIVE"] = "false";
                    _armorServer.Clear();
                }

                if (_vault is not null)
                {
                    string vault = Path.Combine(home, "vault");
                    Directory.CreateDirectory(vault);
                    foreach ((string name, string content) in _vault)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(vault, name))!);
                        File.WriteAllText(Path.Combine(vault, name), content);
                    }

                    FakeCliInstaller.Install(Path.Combine(home, "fakebin"), "op", "bw");
                    env["PATH"] = Path.Combine(home, "fakebin") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
                    env["FAKE_SECRET_CLI_DIR"] = vault;
                    env["BW_SESSION"] = string.Empty;
                }

                if (git)
                {
                    Git(env, "init", "-q");
                }

                if (prepare is not null && await Pty(true, env, prepare, prepareArgs!) is null)
                {
                    return (null, string.Empty);
                }

                JsonObject? screens = await Pty(node, env, steps, args);
                if (_armorServer is not null)
                {
                    await Task.Delay(1500); // the detached events worker
                    _armorRequests = string.Join("\n", _armorServer.RequestLog().Where(line => !line.Contains("/api/events")));
                }

                string requests = _armorServer is null ? "" : "\n--- armor requests\n" +
                    System.Text.RegularExpressions.Regex.Replace(_armorRequests, "[0-9a-f]{64,66}", "<hex>");

                return (screens, Snapshot() + requests);
            }

            (JsonObject? nodeScreens, string nodeTree) = await Side(true);
            if (nodeScreens is null)
            {
                return;
            }

            (JsonObject? dotnetScreens, string dotnetTree) = await Side(false);

            string dump = Environment.GetEnvironmentVariable("PTY_DUMP") ?? string.Empty;
            if (dump.Length > 0)
            {
                string name = System.Text.RegularExpressions.Regex.Replace(string.Join('_', args), "[^A-Za-z0-9_-]+", "") + "-" + Guid.NewGuid().ToString("N")[..6];
                File.WriteAllText(Path.Combine(dump, name + ".node.txt"), Describe(nodeScreens));
                File.WriteAllText(Path.Combine(dump, name + ".port.txt"), Describe(dotnetScreens!));
            }

            AssertSame(Describe(nodeScreens), Describe(dotnetScreens!), string.Join(' ', args));
            dotnetTree.ShouldBe(nodeTree, string.Join(' ', args));
        }
        finally
        {
            ForceDelete(home);
        }
    }

    private static void AssertSame(string want, string got, string label)
    {
        if (want == got)
        {
            return;
        }

        string[] expected = want.Split(Environment.NewLine);
        string[] actual = got.Split(Environment.NewLine);
        int at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at])
        {
            at++;
        }

        string Line(string[] lines) => at < lines.Length ? lines[at] : "<end>";
        string screen = expected.Take(at + 1).LastOrDefault(line => line.StartsWith("== ")) ?? string.Empty;
        throw new Xunit.Sdk.XunitException(label + ": first difference at line " + at + " (" + screen + ")" + Environment.NewLine +
            "node: " + Line(expected) + Environment.NewLine + "port: " + Line(actual));
    }

    private static string Describe(JsonObject run)
    {
        var text = new System.Text.StringBuilder();
        int index = 0;
        foreach (JsonNode? screen in run["snapshots"]!.AsArray())
        {
            text.AppendLine($"== screen {index++}");
            foreach (JsonNode? line in screen!.AsArray())
            {
                text.AppendLine(line!.GetValue<string>());
            }
        }

        text.AppendLine("== final");
        foreach (JsonNode? line in run["final"]!.AsArray())
        {
            text.AppendLine(line!.GetValue<string>());
        }

        bool killed = run["timedOut"]!.GetValue<bool>();
        text.AppendLine(killed ? "== exit (killed after the timeout)" : $"== exit {run["exitCode"]}");

        return text.ToString();
    }

    private static void ForceDelete(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, true);
    }

    // ---- protect -----------------------------------------------------------------------------------------------------

    private static readonly Step[] ProtectDefaults = [Wait("Set protections", Down + Down + Enter)];

    [Fact]
    public Task ProtectAcceptsTheDefaults() => Compare(Files(), true, null, null, ProtectDefaults, "protect");

    [Fact]
    public Task ProtectTogglesTheFirstProtectionOff() =>
        Compare(Files(), true, null, null, [Wait("Set protections", " "), Send(Down + Down), Send(Enter)], "protect");

    [Fact]
    public Task ProtectTogglesBothOff() =>
        Compare(Files(), true, null, null, [Wait("Set protections", " " + Down + " "), Send(Down + Enter)], "protect");

    [Fact]
    public Task ProtectEnterTogglesTheFocusedProtection() =>
        Compare(Files(), true, null, null, [Wait("Set protections", Enter), Send(Down + Down + Enter)], "protect");

    [Fact]
    public Task ProtectCtrlCCancels() => Compare(Files(), true, null, null, [Wait("Set protections", CtrlC)], "protect");

    [Fact]
    public Task ProtectEscapeKeyIsIgnored() =>
        Compare(Files(), true, null, null, [Wait("Set protections", Esc), Send(CtrlC)], "protect");

    [Fact]
    public Task ProtectArrowsWrapAndToggleWithAllNone() =>
        Compare(Files(), true, null, null, [Wait("Set protections", Up), Send(Up), Send("a"), Send("a"), Send("i"), Send(Down + Down + Enter)], "protect");

    [Fact]
    public Task ProtectOutsideAGitRepositoryReports() => Compare(Files(), false, null, null, ProtectDefaults, "protect");

    // The atomic writes protect makes (fsx writeFileXSync): removing the installer's hook block, and rewriting the legacy filter name in
    // .git/info/attributes. Each starts from files that already exist, so the replacement must keep what the file holds besides the block.
    private static readonly string HookBlock = global::Dotenvx.Interop.DotenvxJs.Helpers.InstallPrecommitHook.HookScript;

    private static string LegacyHook => HookBlock.Replace("dotenvx precommit", "dotenvx ext precommit");

    private static readonly string[] LegacyAttributes =
    [
        ".env* filter=dotenvx\n*.env filter=dotenvx\n.flaskenv filter=dotenvx\n.dev.vars* filter=dotenvx\n**/.env.d/* filter=dotenvx\n",
        "# mine\n.env* filter=dotenvx\n*.env filter=dotenvx\n.flaskenv filter=dotenvx\n.dev.vars* filter=dotenvx\n**/.env.d/* filter=dotenvx\n*.png binary\n",
        ".env* filter=dotenvx\r\n*.env filter=dotenvx\r\n.flaskenv filter=dotenvx\r\n.dev.vars* filter=dotenvx\r\n**/.env.d/* filter=dotenvx\r\n",
        "*.png binary\n"
    ];

    [Fact]
    public Task ProtectRemovesAHookThatHoldsOnlyTheInstallerBlock() =>
        Compare(Files(".git/hooks/pre-commit", HookBlock), true, null, null, ProtectDefaults, "protect");

    [Fact]
    public Task ProtectKeepsTheRestOfAHookAfterRemovingTheInstallerBlock() =>
        Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\necho mine\n" + HookBlock), true, null, null, ProtectDefaults, "protect");

    [Fact]
    public Task ProtectRemovesTheLegacyHookBlockAndKeepsTheRest() =>
        Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\necho mine\n" + LegacyHook + "echo after\n"), true, null, null, ProtectDefaults, "protect");

    [Fact]
    public Task ProtectRemovesACrlfHookBlock() =>
        Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\r\necho mine\r\n" + HookBlock.Replace("\n", "\r\n")), true, null, null, ProtectDefaults, "protect");

    [Fact]
    public Task ProtectLeavesAHookThatOnlyMentionsDotenvxAlone() =>
        Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\ndotenvx precommit\n"), true, null, null, ProtectDefaults, "protect");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ProtectRewritesTheLegacyFilterNameInInfoAttributes(int variant) =>
        Compare(Files(".git/info/attributes", LegacyAttributes[variant]), true, null, null, ProtectDefaults, "protect");

    [Fact]
    public Task ProtectWhenAlreadyConfiguredOffersDone() =>
        Compare(Files(), true, ProtectDefaults, ["protect"], [Wait("Set protections", Down + Down), Send(Enter)], "protect");

    [Fact]
    public Task ProtectWhenConfiguredAndChangedOffersApply() =>
        Compare(Files(), true, ProtectDefaults, ["protect"], [Wait("Set protections", " "), Send(Down + Down), Send(Enter)], "protect");

    [Fact]
    public Task ProtectWhenConfiguredRemovesEverything() =>
        Compare(Files(), true, ProtectDefaults, ["protect"], [Wait("Set protections", " " + Down + " "), Send(Down + Enter)], "protect");

    // ---- spec ----------------------------------------------------------------------------------------------------------

    private static Dictionary<string, string> SpecFiles() =>
        Files(".env", "HELLO=World\nFOO=bar\n", ".env.production", "PROD=1\n", "src/app.js", "const a = process.env.FROM_CODE\n");

    /// <summary>A project with source in every language the scanner knows, plus the places it must not look.</summary>
    private static Dictionary<string, string> SourceTree() => Files(
        ".env", "HELLO=World\n",
        ".gitignore", "ignored/\n*.skip.js\n!keep.skip.js\n",
        "src/a.js", "const a = process.env.JS_DOT\nconst b = process?.env?.JS_OPT\nconst c = process.env['JS_BRACKET']\nconst { JS_DESTR, 'JS_QUOTED': q, [\"JS_COMPUTED\"]: z = 1, ...rest } = process.env\n// process.env.JS_COMMENT\nconst env = process.env\nconst d = env.JS_ALIAS\nconst e = import.meta.env.JS_META\nfoo.process.env.JS_NOT\n",
        "src/b.ts", "const x: string = process.env.TS_KEY!\nconst DOTENV_PRIVATE_KEY_X = process.env.DOTENV_PRIVATE_KEY_X\nconst p = process.env.DOTENV_PUBLIC_KEY\n",
        "src/c.vue", "<script>\nconst v = process.env.VUE_KEY\n</script>\n",
        "src/d.py", "import os\nimport os as operating\nfrom os import getenv, environ as e\nx = os.getenv('PY_GETENV')\ny = os.environ['PY_BRACKET']\nz = os.environ.get('PY_GET')\nw = operating.getenv(\"PY_ALIAS\")\nq = getenv('PY_FROM')\nr = e['PY_ENVIRON']\n# os.getenv('PY_COMMENT')\n",
        "src/e.go", "package main\nimport (\n\t\"os\"\n\tenvs \"os\"\n)\nfunc main() {\n\t_ = os.Getenv(\"GO_GETENV\")\n\t_, _ = os.LookupEnv(\"GO_LOOKUP\")\n\t_ = envs.Getenv(\"GO_ALIAS\")\n}\n",
        "src/f.rb", "a = ENV['RB_BRACKET']\nb = ENV.fetch('RB_FETCH', 'x')\nc = ENV.fetch 'RB_FETCH_SPACE'\n# ENV['RB_COMMENT']\n",
        "src/g.php", "<?php\n$a = getenv('PHP_GETENV');\n$b = $_ENV['PHP_ENV'];\n$c = $_SERVER[\"PHP_SERVER\"];\n",
        "src/h.rs", "use std::env;\nuse std::env::{var as v, var_os};\nfn main() {\n    let a = env::var(\"RS_VAR\");\n    let b = std::env::var_os(\"RS_OS\");\n    let c = v(\"RS_ALIAS\");\n    let d = var_os(\"RS_USED\");\n}\n",
        "src/I.java", "class I { String a = System.getenv(\"JAVA_KEY\"); String b = java.lang.System.getenv(\"JAVA_FULL\"); }\n",
        "src/j.cs", "class J { string a = Environment.GetEnvironmentVariable(\"CS_KEY\"); string b = System.Environment.GetEnvironmentVariable(\"CS_FULL\"); }\n",
        "ignored/k.js", "process.env.IGNORED_DIR\n",
        "other.skip.js", "process.env.IGNORED_GLOB\n",
        "keep.skip.js", "process.env.UNIGNORED\n",
        "node_modules/m/index.js", "process.env.IN_NODE_MODULES\n",
        "sub/package.json", "{}\n",
        "sub/n.js", "process.env.IN_SUBPROJECT\n",
        "notes.txt", "process.env.NOT_SOURCE\n");

    [Fact]
    public Task SpecScansSourceInEveryLanguage() =>
        Compare(SourceTree(), false, null, null, [Wait("Create Envfile from", Up), Send(Enter)], "spec");

    [Fact]
    public Task SpecAcceptsEverything() =>
        Compare(SpecFiles(), false, null, null, [Wait("Create Envfile from", Up), Send(Enter)], "spec");

    [Fact]
    public Task SpecWithoutCode() =>
        Compare(SpecFiles(), false, null, null, [Wait("Create Envfile from", Down + Down + " "), Send(Down + Enter)], "spec");

    [Fact]
    public Task SpecWithNothingSelected() =>
        Compare(SpecFiles(), false, null, null, [Wait("Create Envfile from", " " + Down + " " + Down + " "), Send(Down + Enter)], "spec");

    [Fact]
    public Task SpecCtrlCCancels() => Compare(SpecFiles(), false, null, null, [Wait("Create Envfile from", CtrlC)], "spec");

    [Fact]
    public Task SpecOverwriteRecreates() =>
        Compare(new Dictionary<string, string>(SpecFiles()) { ["Envfile"] = "env \"HELLO\"\n" }, false, null, null,
            [Wait("Recreate Envfile from", Up), Send(Enter)], "spec", "--overwrite");

    [Fact]
    public Task SpecWithoutEnvFiles() =>
        Compare(Files("src/app.js", "const a = process.env.FROM_CODE\n"), false, null, null, [Wait("Create Envfile from", Up), Send(Enter)], "spec");

    [Fact]
    public Task SpecWithManyEnvFilesScrolls() =>
        Compare(Files(Enumerable.Range(0, 30).SelectMany(i => new[] { $".env.e{i:00}", $"K{i}=1\n" }).ToArray()), false, null, null,
            [Wait("Create Envfile from", Up), Send(Up + Up), Send(Down + Down + Down), Send("a"), Send(Up + Enter)], "spec");

    // ---- login (listenForOpenKey.js) -------------------------------------------------------------------------------------

    /// <summary>
    /// The device-flow Armor API: the token endpoint answers "pending" for the first <paramref name="pending" /> polls. The
    /// "complete" verification uri is not a web address, so pressing Enter never launches anything on either side.
    /// </summary>
    private static FakeArmorServer LoginServer(int pending)
    {
        int polls = 0;
        var server = new FakeArmorServer();
        server.Handler = request =>
        {
            if (request.Path == "/oauth/device/code")
            {
                // every login starts a fresh flow: the Node run and the .NET run share this server
                Interlocked.Exchange(ref polls, 0);

                return new FakeResponse(200,
                    """{"device_code":"dev123","user_code":"ABCD1234","verification_uri":"https://armor.example/device","verification_uri_complete":"not-a-web-address","interval":1}""");
            }

            return Interlocked.Increment(ref polls) <= pending
                ? new FakeResponse(400, """{"error":"authorization_pending","error_description":"pending"}""")
                : new FakeResponse(200, """{"access_token":"tok_new_123456","id":"usr_9","username":"bob"}""");
        };

        return server;
    }

    private async Task LoginWith(int pending, Step[] steps)
    {
        using FakeArmorServer server = LoginServer(pending);
        await Compare(Files(), false, null, null, steps, "login", "--verbose", "--hostname", server.BaseUrl);
    }

    [Fact]
    public Task LoginEnterOpensThenCompletes() => LoginWith(2, [Wait("press Enter to open", Enter)]);

    [Fact]
    public Task LoginYOpensThenCompletes() => LoginWith(2, [Wait("press Enter to open", "y")]);

    [Fact]
    public Task LoginUpperYOpensThenCompletes() => LoginWith(2, [Wait("press Enter to open", "Y")]);

    [Fact]
    public Task LoginNewlineOpensOnlyOnce() => LoginWith(3, [Wait("press Enter to open", Enter), Send(Enter), Send("y")]);

    [Fact]
    public Task LoginOtherKeysAreIgnored() => LoginWith(2, [Wait("press Enter to open", "x" + "q" + Up), Send(Enter)]);

    [Fact]
    public Task LoginNCancels() => LoginWith(30, [Wait("press Enter to open", "n")]);

    [Fact]
    public Task LoginUpperNCancels() => LoginWith(30, [Wait("press Enter to open", "N")]);

    [Fact]
    public Task LoginCtrlCCancels() => LoginWith(30, [Wait("press Enter to open", CtrlC)]);

    [Fact]
    public Task LoginWithoutKeys() => LoginWith(1, [Wait("press Enter to open", "")]);

    // ---- set -----------------------------------------------------------------------------------------------------------

    [Fact]
    public Task SetPromptsForThePlainValue() =>
        Compare(Files(".env", "A=1\n"), false, null, null, [Wait("KEY", "secret value" + Enter)], "set", "KEY", "--plain");

    [Fact]
    public Task SetPromptEmptyValue() =>
        Compare(Files(".env", "A=1\n"), false, null, null, [Wait("KEY", Enter)], "set", "KEY", "--plain");

    [Fact]
    public Task SetPromptCancelled() =>
        Compare(Files(".env", "A=1\n"), false, null, null, [Wait("KEY", "abc" + CtrlC)], "set", "KEY", "--plain");

    [Fact]
    public Task SetPromptEditing() =>
        Compare(Files(".env", "A=1\n"), false, null, null, [Wait("KEY", "abcd" + Left + "\u007f" + "X" + Enter)], "set", "KEY", "--plain");

    // ---- key custody -----------------------------------------------------------------------------------------------------

    [Fact]
    public Task CustodyMenuLocalThenBack() =>
        Compare(Files(".env", "A=1\n"), false, null, null,
            [Wait("Choose private key custody", Enter), Wait("Choose local custody", Left), new Step(null, Down, 1500), new Step(null, Up, 1500)], "set", "A", "1");

    [Fact]
    public Task CustodyMenuCtrlC() =>
        Compare(Files(), false, null, null, [Wait("Choose private key custody", CtrlC)], "set", "A", "1");

    [Fact]
    public Task CustodyMenuEscape() =>
        Compare(Files(), false, null, null, [Wait("Choose private key custody", Esc)], "set", "A", "1");

    [Fact]
    public Task CustodyMenuNavigatesToArmorSignIn() =>
        Compare(Files(), false, null, null, [Wait("Choose private key custody", Down + Enter)], "set", "A", "1");

    [Fact]
    public Task CustodyMenuChoosesLocalFile() =>
        Compare(Files(), false, null, null, [Wait("Choose private key custody", Enter), Wait("Choose local custody", Enter)], "set", "A", "1");

    // ---- Bitwarden unlock prompt ----------------------------------------------------------------------------------------------

    private static readonly Dictionary<string, string> LockedVault = new()
    {
        ["session"] = "unlocked-session", ["master"] = "master-pw", ["bw-status"] = "locked"
    };

    private Task BitwardenPrompt(Step[] steps, params string[] args)
    {
        _vault = LockedVault;

        return Compare(Files(".env", FixedKeypairFixture.EncryptedEnv, ".env.keys", FixedKeypairFixture.PrivateKeyFile), false, null, null, steps, args);
    }

    [Fact]
    public Task BitwardenUnlockWithMasterPassword() =>
        BitwardenPrompt([Wait("Bitwarden master password", "master-pw" + Enter)], "bitwarden", "up", "-f", ".env");

    [Fact]
    public Task BitwardenUnlockWrongPassword() =>
        BitwardenPrompt([Wait("Bitwarden master password", "nope" + Enter)], "bitwarden", "up", "-f", ".env");

    [Fact]
    public Task BitwardenUnlockCancelled() =>
        BitwardenPrompt([Wait("Bitwarden master password", CtrlC)], "bitwarden", "up", "-f", ".env");

    [Fact]
    public Task BitwardenUnlockEmptyPassword() =>
        BitwardenPrompt([Wait("Bitwarden master password", Enter)], "bitwarden", "up", "-f", ".env");

    [Fact]
    public Task BitwardenUnlockOnGet() =>
        BitwardenPrompt([Wait("Bitwarden master password", "master-pw" + Enter)], "bitwarden", "down", "-f", ".env");

    // ---- Bitwarden secret references (resolveBitwardenPassword.js asks for the master password when BW_SESSION is empty) ----

    private Task ReferencePrompt(Step[] steps, params string[] args)
    {
        _vault = new Dictionary<string, string>(LockedVault) { ["bw/github_password"] = "bw-secret" };

        return Compare(Files(".env", "A=bw://github/password\nB=plain\n"), false, null, null, steps, args);
    }

    [Fact]
    public Task SecretReferenceUnlocksWithMasterPassword() =>
        ReferencePrompt([Wait("Bitwarden master password", "master-pw" + Enter)], "get");

    [Fact]
    public Task SecretReferenceWrongMasterPassword() =>
        ReferencePrompt([Wait("Bitwarden master password", "nope" + Enter)], "get");

    [Fact]
    public Task SecretReferenceEmptyMasterPassword() =>
        ReferencePrompt([Wait("Bitwarden master password", Enter)], "get");

    [Fact]
    public Task SecretReferenceMasterPasswordCancelled() =>
        ReferencePrompt([Wait("Bitwarden master password", CtrlC)], "get");

    [Fact]
    public Task SecretReferenceUnlocksOnRun() =>
        ReferencePrompt([Wait("Bitwarden master password", "master-pw" + Enter)], "run", "--", "node", "-e", "console.log(process.env.A)");

    [Fact]
    public Task SecretReferenceCancelledOnRun() =>
        ReferencePrompt([Wait("Bitwarden master password", CtrlC)], "run", "--", "node", "-e", "console.log(process.env.A)");

    // ---- Armor custody (custodians/managed/armor/store.js) -----------------------------------------------------------------

    private async Task ArmorCustody(Func<RecordedRequest, FakeResponse> handler, Step[] steps, params string[] args)
    {
        using var server = new FakeArmorServer { Handler = handler };
        _armorServer = server;
        try
        {
            await Compare(Files(), false, null, null, steps, args);
        }
        finally
        {
            _armorServer = null;
        }
    }

    private static FakeResponse TeamRequired(RecordedRequest request, params string[] teams) =>
        request.Body.Contains("\"team\":\"") || request.Path != "/api/armor/up"
            ? new FakeResponse(200, "{}")
            : new FakeResponse(400, "{\"error\":{\"code\":\"TEAM_REQUIRED\",\"message\":\"team required\",\"meta\":{\"organizations\":[" +
                string.Join(",", teams.Select(team => "{\"provider_slug\":\"" + team + "\"}")) + "]}}}");

    [Fact]
    public Task ArmorCustodyStoresDirectly() =>
        ArmorCustody(_ => new FakeResponse(200, "{}"), [Wait("Choose private key custody", Down + Enter)], "set", "A", "1");

    [Fact]
    public Task ArmorCustodyAsksForTheTeam() =>
        ArmorCustody(request => TeamRequired(request, "acme", "beta"), [Wait("Choose private key custody", Down + Enter), Wait("Select team", Down + Enter)], "set", "A", "1");

    [Fact]
    public Task ArmorCustodyUsesTheOnlyTeam() =>
        ArmorCustody(request => TeamRequired(request, "acme"), [Wait("Choose private key custody", Down + Enter)], "set", "A", "1");

    [Fact]
    public Task ArmorCustodyTeamPromptCancelled() =>
        ArmorCustody(request => TeamRequired(request, "acme", "beta"), [Wait("Choose private key custody", Down + Enter), Wait("Select team", CtrlC)], "set", "A", "1");

    [Fact]
    public Task ArmorCustodyServerError() =>
        ArmorCustody(_ => new FakeResponse(500, "{\"error\":{\"code\":\"BOOM\",\"message\":\"nope\"}}"), [Wait("Choose private key custody", Down + Enter)], "set", "A", "1");

    // ---- armor up/down/push/pull/move asking for the team (services/armor{Up,Down,Push,Pull,Move}.js) -----------------------

    private static FakeResponse TeamRequiredFor(RecordedRequest request, string op, params string[] teams) =>
        request.Body.Contains("\"team\":\"") || request.Path != "/api/armor/" + op
            ? new FakeResponse(200, "{}")
            : new FakeResponse(400, "{\"error\":{\"code\":\"TEAM_REQUIRED\",\"message\":\"team required\",\"meta\":{\"organizations\":[" +
                string.Join(",", teams.Select(team => "{\"provider_slug\":\"" + team + "\"}")) + "]}}}");

    private async Task ArmorTeam(string op, string[] teams, Step[] steps)
    {
        Dictionary<string, string> files = Files(".env", FixedKeypairFixture.EncryptedEnv, ".env.keys", FixedKeypairFixture.PrivateKeyFile);
        using var server = new FakeArmorServer { Handler = request => TeamRequiredFor(request, op, teams) };
        _armorServer = server;
        try
        {
            await Compare(files, false, null, null, steps, "armor", op);
        }
        finally
        {
            _armorServer = null;
        }
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("push")]
    [InlineData("pull")]
    [InlineData("move")]
    public async Task ArmorOperationAsksForTheTeam(string op) =>
        await ArmorTeam(op, ["acme", "beta"], [Wait("Select team", Down + Enter)]);

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("push")]
    [InlineData("pull")]
    [InlineData("move")]
    public async Task ArmorOperationUsesTheOnlyTeam(string op) =>
        await ArmorTeam(op, ["acme"], []);

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    [InlineData("push")]
    [InlineData("pull")]
    [InlineData("move")]
    public async Task ArmorOperationTeamPromptCancelled(string op) =>
        await ArmorTeam(op, ["acme", "beta"], [Wait("Select team", CtrlC)]);

    [Fact]
    public Task SecretReferenceUnlocksOnCheck() =>
        ReferencePrompt([Wait("Bitwarden master password", "master-pw" + Enter)], "check");

    [Fact]
    public Task SecretReferenceCancelledOnCheck() =>
        ReferencePrompt([Wait("Bitwarden master password", CtrlC)], "check");

    [Fact]
    public Task SecretReferenceCancelledOnSet() =>
        ReferencePrompt([Wait("Bitwarden master password", CtrlC)], "set", "NEW", "1", "--plain");

    // ---- feedback ------------------------------------------------------------------------------------------------------

    [Fact]
    public Task FeedbackPromptCancelled() =>
        Compare(Files(), false, null, null, [Wait("Send feedback", "hello" + CtrlC)], "feedback");

    [Fact]
    public Task FeedbackEmptyMessageIsRejected() =>
        Compare(Files(), false, null, null, [Wait("Send feedback", Enter)], "feedback");
}
