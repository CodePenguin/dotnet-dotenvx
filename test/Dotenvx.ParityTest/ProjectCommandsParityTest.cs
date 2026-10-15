using System.Diagnostics;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - coverage-guided (scripts/parity-coverage.js): `gitignore`, `genexample`, `prebuild`, `precommit`,
/// `check` and `spec` were only covered by ported unit tests. Each scenario runs the real Node CLI and the .NET CLI from the
/// same on-disk state (optionally a fresh git repository) and compares exit code, stdout, stderr and the resulting file tree.
/// </remarks>
public class ProjectCommandsParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string Plain = "HELLO=World\nFOO=bar\n";

    private static readonly string Encrypted = FixedKeypairFixture.EncryptedEnv;

    private static Dictionary<string, string> Env(string home)
    {
        return new Dictionary<string, string>
        {
            ["GIT_CONFIG_GLOBAL"] = Path.Combine(home, "gitconfig"),
            ["GIT_CONFIG_SYSTEM"] = Path.Combine(home, "gitconfig-system"),
            ["XDG_CONFIG_HOME"] = Path.Combine(home, "xdg"),
            ["GIT_CONFIG_NOSYSTEM"] = "1"
        };
    }

    private void Git(Dictionary<string, string> env, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = WorkDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (string arg in new[] { "-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false" }.Concat(args))
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

    /// <summary>Every file in the working directory (the git hook included, the rest of .git excluded) with its content.</summary>
    private string Snapshot()
    {
        var lines = new List<string>();
        foreach (string file in Directory.EnumerateFiles(WorkDir, "*", SearchOption.AllDirectories).Order())
        {
            string relative = Path.GetRelativePath(WorkDir, file).Replace('\\', '/');
            if (relative.StartsWith(".git/", StringComparison.Ordinal) && relative != ".git/hooks/pre-commit")
            {
                continue;
            }

            lines.Add($"--- {relative}\n{File.ReadAllText(file)}");
        }

        return string.Join("\n", lines);
    }

    private async Task Compare(Dictionary<string, string> files, bool git, string[]? staged, params string[] args)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string home = Path.Combine(Path.GetTempPath(), "dotenvx-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            Dictionary<string, string> env = Env(home);

            void Setup()
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

                foreach ((string name, string content) in files)
                {
                    WriteFile(name, content);
                }

                if (git)
                {
                    Git(env, "init", "-q");
                    if (staged is not null)
                    {
                        Git(env, [.. new[] { "add", "-f", "--" }, .. staged]);
                    }
                }
            }

            Setup();
            CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);
            string nodeTree = Snapshot();

            Setup();
            CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);
            string dotnetTree = Snapshot();

            dotnet.ExitCode.ShouldBe(node.ExitCode, $"{string.Join(' ', args)}\nnode out: {node.StandardOutput}\nnode err: {node.StandardError}\ndotnet out: {dotnet.StandardOutput}\ndotnet err: {dotnet.StandardError}");
            dotnet.StandardOutput.ShouldBe(node.StandardOutput);
            NativeWording(dotnet.StandardError).ShouldBe(NativeWording(node.StandardError));
            dotnetTree.ShouldBe(nodeTree);
        }
        finally
        {
            ForceDelete(home);
        }
    }

    // native OS/runtime wording is not dotenvx's own (random temp-file suffix, ENOENT vs .NET's file-not-found text)
    private static string NativeWording(string text)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\.\d{6,}'", "'");

        return System.Text.RegularExpressions.Regex.Replace(text, @"Could not find file ('[^']+')\.",
            "ENOENT: no such file or directory, open $1");
    }

    private static void ForceDelete(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, true);
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

    [Theory]
    [InlineData("gitignore")]
    [InlineData("ext", "gitignore")]
    [InlineData("gitignore", "--pattern", ".env.keys")]
    [InlineData("gitignore", "--pattern", ".env*", ".env.keys", "extra")]
    public async Task GitignoreCreatesTheFileLikeNode(params string[] args)
    {
        await Compare(Files(), false, null, args);
    }

    [Theory]
    [InlineData("gitignore", ".gitignore", "node_modules\n")]
    [InlineData("gitignore", ".gitignore", ".env*\n")]
    [InlineData("gitignore", ".gitignore", "no-trailing-newline")]
    [InlineData("gitignore", ".gitignore", "")]
    [InlineData("gitignore", ".dockerignore", ".env*\n")]
    [InlineData("gitignore", ".dockerignore", "build\n")]
    [InlineData("gitignore", ".npmignore", "src\n")]
    [InlineData("gitignore", ".vercelignore", "docs\n")]
    public async Task GitignoreAppendsToExistingIgnoreFilesLikeNode(string command, string file, string content)
    {
        await Compare(Files(file, content), false, null, command);
        await Compare(Files(file, content, ".gitignore", "a\n"), false, null, command, "--pattern", ".env.keys");
    }

    [Theory]
    [InlineData("genexample")]
    [InlineData("ext", "genexample")]
    [InlineData("genexample", "-f", ".env.other")]
    [InlineData("genexample", "-f", ".env", ".env.other")]
    [InlineData("genexample", "sub")]
    [InlineData("genexample", "missing-dir")]
    [InlineData("genexample", "-f", "nope.env")]
    public async Task GenexampleMatchesNode(params string[] args)
    {
        Dictionary<string, string> files = Files(".env", Plain + "SECRET=\"quoted value\"\n", ".env.other", "OTHER=1\nHELLO=Again\n",
            "sub/.env", "SUB=1\n", "sub/.env.production", "PROD=2\n", ".env.keys", "DOTENV_PRIVATE_KEY=x\n");
        await Compare(files, false, null, args);
    }

    [Theory]
    [InlineData(".env.example", "HELLO=World\n")]
    [InlineData(".env.example", "HELLO=World\nEXTRA=1\n")]
    [InlineData(".env.example", "")]
    [InlineData(".env.example", "NO_NEWLINE=1")]
    [InlineData("none", "")]
    public async Task GenexampleExistingExampleMatchesNode(string name, string content)
    {
        Dictionary<string, string> files = Files(".env", Plain + "NEWKEY=1\n");
        if (name != "none")
        {
            files[name] = content;
        }

        await Compare(files, false, null, "genexample");
        await Compare(Files(".env.previous", "A=1\n", ".env.project", "B=1\n", ".env.x", "C=1\n"), false, null, "genexample");
        await Compare(Files(), false, null, "genexample");
    }

    [Theory]
    [InlineData("prebuild")]
    [InlineData("ext", "prebuild")]
    [InlineData("prebuild", "sub")]
    [InlineData("prebuild", "missing")]
    public async Task PrebuildMatchesNode(params string[] args)
    {
        string[][] fixtures =
        [
            [],
            [".env", Plain],
            [".env", Encrypted],
            [".env", Plain, ".dockerignore", ".env*\n"],
            [".env", Plain, ".dockerignore", "node_modules\n"],
            [".env", Plain, ".dockerignore", ".env\n"],
            [".env.example", Plain, ".dockerignore", ".env*\n"],
            [".env.example", Plain, ".dockerignore", "node_modules\n"],
            [".env.vault", Plain, ".dockerignore", "node_modules\n"],
            [".env.x", Plain],
            [".env.keys", "DOTENV_PRIVATE_KEY=abc\n"],
            [".env.keys", "DOTENV_PRIVATE_KEY=abc\n", ".dockerignore", ".env.keys\n"],
            ["sub/.env", Plain],
            ["sub/.env", Encrypted, "sub/.env.example", Plain],
            [".env.production", Encrypted + "PLAIN_PLAIN=\"x\"\n"],
            [".env", Plain + "A_PLAIN=1\nDOTENV_PUBLIC_KEY=abc\n"],
            [".env", Plain, "test/.env", Plain, "tests/.env", Plain],
            [".env", Encrypted, ".dockerignore", ".env\n"]
        ];
        foreach (string[] fixture in fixtures)
        {
            await Compare(Files(fixture), false, null, args);
        }
    }

    [Theory]
    [InlineData("precommit")]
    [InlineData("ext", "precommit")]
    [InlineData("precommit", "sub")]
    [InlineData("precommit", "missing")]
    public async Task PrecommitMatchesNode(params string[] args)
    {
        string[][] fixtures =
        [
            [],
            [".env", Plain],
            [".env", Encrypted],
            [".env", Plain, ".gitignore", ".env*\n"],
            [".env", Plain, ".gitignore", "node_modules\n"],
            [".env.example", Plain, ".gitignore", "node_modules\n"],
            [".env.example", Plain, ".gitignore", ".env*\n"],
            [".env.keys", "DOTENV_PRIVATE_KEY=abc\n"],
            [".env.keys", "DOTENV_PRIVATE_KEY=abc\n", ".gitignore", ".env.keys\n"],
            ["sub/.env", Plain],
            ["sub/.env", Encrypted, "sub/.env.example", Plain, ".gitignore", "x\n"],
            [".env.production", Plain + "A_PLAIN=1\n"]
        ];
        foreach (string[] fixture in fixtures)
        {
            await Compare(Files(fixture), false, null, args);
            await Compare(Files(fixture), true, null, args);
            await Compare(Files(fixture), true, [.. fixture.Where((_, i) => i % 2 == 0)], args);
        }
    }

    [Fact]
    public async Task PrecommitWithNonAsciiAndUntrackedFilesMatchesNode()
    {
        await Compare(Files("café/.env", Plain, "plain/.env", Plain, ".gitignore", "x\n"), true, ["café/.env"], "precommit");
        await Compare(Files("café/.env", Plain, "plain/.env", Plain, ".gitignore", "x\n"), true, ["plain/.env"], "precommit");
    }

    [Theory]
    [InlineData("--install")]
    [InlineData("-i")]
    [InlineData("--uninstall")]
    public async Task PrecommitHookInstallationMatchesNode(string option)
    {
        Dictionary<string, string> none = Files();
        await Compare(none, true, null, "precommit", option);
        await Compare(none, false, null, "precommit", option);
        await Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\necho hi\n"), true, null, "precommit", option);
        await Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\ndotenvx precommit\n"), true, null, "precommit", option);
        await Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\ndotenvx ext precommit\n"), true, null, "precommit", option);
        await Compare(Files(".git/hooks/pre-commit", "#!/bin/sh\n# >>> dotenvx\ndotenvx protect\n# <<< dotenvx\n"), true, null, "precommit", option);
        await Compare(none, true, null, "ext", "precommit", option);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("check", "-f", ".env.other")]
    [InlineData("check", "-f", "missing.env")]
    [InlineData("check", "--ignore", "MISSING_ENV_FILE", "-f", "missing.env")]
    [InlineData("check", "-e", "HELLO=World")]
    [InlineData("check", "--profile", "production")]
    [InlineData("check", "--profile", "missing")]
    [InlineData("check", "--convention", "nextjs")]
    [InlineData("check", "--no-armor", "--no-native", "--no-1password", "--no-bitwarden")]
    [InlineData("check", "-o")]
    public async Task CheckMatchesNode(params string[] args)
    {
        string[][] fixtures =
        [
            [],
            [".env", Plain],
            [".env", Plain, "Envfile", "env \"HELLO\"\n"],
            [".env", Plain, "Envfile", "env \"HELLO\"\nenv \"MISSING\"\n"],
            [".env", Plain, "Envfile", "env \"HELLO\"\nenv \"MISSING\", optional: true\n"],
            [".env", Plain, "Envfile", "env \"HELLO\", type: \"email\"\n"],
            [".env", "N=5\nU=http://x.test\nP=99999\nE=blue\nEM=a@b.co\n", "Envfile",
                "env \"N\", type: \"integer\", min: 1, max: 3\nenv \"U\", type: \"url\"\nenv \"P\", type: \"port\"\nenv \"E\", enum: [\"red\", \"green\"]\nenv \"EM\", type: \"email\"\n"],
            [".env", Plain, "Envfile", "strict true\nenv \"HELLO\"\nenv \"MISSING\"\n"],
            [".env", Plain, "Envfile", "this is not valid ((\n"],
            [".env", Encrypted, "Envfile", "env \"HELLO\"\n"],
            [".env", Encrypted, "Envfile", "env \"HELLO\", encrypted: false\n"],
            [".env", Plain, "Envfile", "env \"HELLO\", encrypted: true\n"],
            [".env", Plain, ".env.other", "X=1\n", "Envfile", "env \"X\"\n"],
            [".env", Plain, ".env.production", "FOO=1\n", "Envfile",
                "env \"HELLO\"\nprofile \"production\", files: [\".env.production\"]\nfile \".env.production\" do\n  env \"FOO\", required: true\n  env \"BAR\"\nend\n"],
            [".env", Plain, ".env.other", "HELLO=2\nBAD=\"a b\"\nCTL=\"x\\ty\"\n", "Envfile",
                "env \"HELLO\"\nenv \"BAD\", type: \"integer\"\nenv \"CTL\"\n"]
        ];
        foreach (string[] fixture in fixtures)
        {
            await Compare(Files(fixture), false, null, args);
        }
    }

    [Theory]
    [InlineData("spec", "--stdout")]
    [InlineData("spec")]
    [InlineData("spec", "--overwrite")]
    [InlineData("spec", "-f", ".env.other", "--stdout")]
    [InlineData("spec", "-f", "missing.env", "--stdout")]
    [InlineData("spec", "-f", "missing.env")]
    public async Task SpecMatchesNode(params string[] args)
    {
        string[][] fixtures =
        [
            [],
            [".env", Plain],
            [".env", Plain, ".env.other", "X=1\nHELLO=2\n"],
            [".env", Encrypted],
            [".env", Plain, "Envfile", "env \"HELLO\"\n# custom\n"],
            [".env", Plain, ".env.production", "PROD=1\n"],
            [".env", "DUP=1\nDUP=2\n#comment\nQUOTED=\"a b\"\n"],
            [".env", Plain, "src/app.js", "const a = process.env.FROM_CODE\nprocess.env['OTHER_CODE']\n"]
        ];
        foreach (string[] fixture in fixtures)
        {
            await Compare(Files(fixture), false, null, args);
        }
    }
}
