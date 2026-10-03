using System.Text.Json;

using Dotenvx.Primitives;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Reference source: dotenvx/tests/e2e/{get,encrypt,decrypt,run,version,ext}.test.js. The upstream e2e tests shell out to
/// the real CLI and assert on its output; each scenario here runs the same steps against BOTH the real Node CLI and this
/// port, asserting the upstream test's expected values on each side. Not ported (out of scope): "#--help shows armor
/// advanced command", "#armor --help shows login and logout commands", "#run --help shows redaction, armor, native, and
/// secret manager flags", "ext missing" and "ext vault" (dynamic `dotenvx-ext-*` extension binaries).
/// </remarks>
public class E2eParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private delegate Task<CliResult> Cli(string[] args, Dictionary<string, string>? env = null);

    /// <summary>Runs the scenario once against Node and once against this port, each from an empty working directory.</summary>
    private async Task ForBothAsync(Func<Cli, Task> scenario)
    {
        foreach (bool useNode in new[] { true, false })
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

            Cli cli = (args, env) => useNode
                ? Fixture.RunNodeAsync(WorkDir, env, args)
                : Fixture.RunDotnetAsync(WorkDir, env, args);
            await scenario(cli);
        }
    }

    private string KeyFromKeysFile(string name)
    {
        return (string)ScanToDictionary(ReadFile(".env.keys"))[name];
    }

    private static string Out(CliResult result)
    {
        return result.StandardOutput.Trim();
    }

    /// <remarks>Refer to "#get"</remarks>
    [Fact]
    public async Task Get()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");

            Out(await cli(["get", "HELLO"])).ShouldBe("World");
        });
    }

    /// <remarks>Refer to "#get --env"</remarks>
    [Fact]
    public async Task GetEnv()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");

            Out(await cli(["get", "HELLO", "--env", "HELLO=String"])).ShouldBe("World");
            Out(await cli(["get", "HELLO", "--env", "HELLO=String", "-f", ".env"])).ShouldBe("String");
            Out(await cli(["get", "HELLO", "-f", ".env", "--env", "HELLO=String"])).ShouldBe("World");
        });
    }

    /// <remarks>Refer to "#get --overload"</remarks>
    [Fact]
    public async Task GetOverload()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");
            WriteFile(".env.production", "HELLO=production\n");

            Out(await cli(["get", "HELLO", "-f", ".env.production", "--env", "HELLO=String", "-f", ".env", "--overload"]))
                .ShouldBe("World");
        });
    }

    /// <remarks>Refer to "#get (json)"</remarks>
    [Fact]
    public async Task GetJson()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");

            Out(await cli(["get"])).ShouldBe("{\"HELLO\":\"World\"}");
        });
    }

    /// <remarks>Refer to "#get --include-key"</remarks>
    [Fact]
    public async Task GetIncludeKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\nHOLA=Mundo\nGOODBYE=World");

            Out(await cli(["get", "-ik", "H*"])).ShouldBe("{\"HELLO\":\"World\",\"HOLA\":\"Mundo\"}");
            Out(await cli(["get", "--include-key", "HELLO", "GOODBYE"]))
                .ShouldBe("{\"HELLO\":\"World\",\"GOODBYE\":\"World\"}");
        });
    }

    /// <remarks>Refer to "#get --exclude-key"</remarks>
    [Fact]
    public async Task GetExcludeKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "DOTENV_PUBLIC_KEY=public\nDOTENV_PUBLIC_KEY_PRODUCTION=production\nHELLO=World");

            Out(await cli(["get", "-ek", "DOTENV_PUBLIC_KEY*"])).ShouldBe("{\"HELLO\":\"World\"}");
            Out(await cli(["get", "--format=shell", "--exclude-key", "DOTENV_PUBLIC_KEY*"])).ShouldBe("HELLO=World");
            Out(await cli(["get", "--format=colon", "-ek", "DOTENV_PUBLIC_KEY*"])).ShouldBe("HELLO:World");
            Out(await cli(["get", "--format=eval", "-ek", "DOTENV_PUBLIC_KEY*"])).ShouldBe("HELLO='World'");
            Out(await cli(["get", "--format=eval-export", "-ek", "DOTENV_PUBLIC_KEY*"]))
                .ShouldBe("export HELLO='World'");
        });
    }

    /// <remarks>Refer to "#get --include-key --exclude-key"</remarks>
    [Fact]
    public async Task GetIncludeKeyExcludeKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\nHOLA=Mundo\nGOODBYE=World");

            Out(await cli(["get", "-ik", "H*", "-ek", "HOLA"])).ShouldBe("{\"HELLO\":\"World\"}");
        });
    }

    /// <remarks>Refer to "#encrypt creates a minimal sample env"</remarks>
    [Fact]
    public async Task EncryptCreatesAMinimalSampleEnv()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            CliResult output = await cli(["encrypt"]);
            Dictionary<string, List<string>> parsed =
                Primitives.Primitives.Scan(ReadFile(".env"), new ScanOptions()).Parsed;

            Out(output).ShouldBe("");
            output.StandardError.Trim().ShouldBe("◈ encrypted (.env)");
            parsed.Keys.Order().ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
            Out(await cli(["get", "HELLO"])).ShouldBe("World");
        });
    }

    /// <remarks>Refer to "#encrypt -k"</remarks>
    [Fact]
    public async Task EncryptKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\nHI=thar\n");

            CliResult output = await cli(["encrypt", "-k", "HI"]);
            string privateKey = KeyFromKeysFile("DOTENV_PRIVATE_KEY");

            Out(output).ShouldBe("");
            output.StandardError.Trim().ShouldBe("◈ encrypted (.env)");

            File.Delete(Path.Combine(WorkDir, ".env.keys"));

            CliResult hello = await cli(["get", "HELLO"]);
            hello.ExitCode.ShouldBe(1); // get exits when another key cannot be decrypted
            Out(hello).ShouldBe("World"); // unencrypted still
            hello.StandardError.ShouldContain("DECRYPTION_FAILED");

            CliResult hi = await cli(["get", "HI"]);
            hi.ExitCode.ShouldBe(1); // get exits when the requested encrypted key cannot be decrypted
            Out(hi).ShouldStartWith("encrypted:");

            CliResult withKey = await cli(["get", "HI"], new Dictionary<string, string> { ["DOTENV_PRIVATE_KEY"] = privateKey });
            Out(withKey).ShouldBe("thar");
        });
    }

    private const string DecryptFailed =
        "☠ [DECRYPTION_FAILED] could not decrypt HELLO. fix: [https://github.com/dotenvx/dotenvx/issues/757]\n";

    /// <remarks>Refer to "#decrypt - missing DOTENV_PRIVATE_KEY"</remarks>
    [Fact]
    public async Task DecryptMissingPrivateKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");
            await cli(["encrypt"]);
            File.Delete(Path.Combine(WorkDir, ".env.keys")); // rm .env.keys prior to running decrypt

            CliResult result = await cli(["decrypt"]);

            result.ExitCode.ShouldBe(1);
            result.StandardOutput.ShouldBe("○ no change (.env)\n");
            result.StandardError.ShouldBe(DecryptFailed);
        });
    }

    /// <remarks>Refer to "#decrypt --stdout"</remarks>
    [Fact]
    public async Task DecryptStdout()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");
            await cli(["encrypt"]);
            string publicKey = Out(await cli(["get", "DOTENV_PUBLIC_KEY"]));

            CliResult result = await cli(["decrypt", "--stdout"]);

            result.StandardOutput.ShouldBe(
                "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/\n" +
                "#/            public-key encryption for .env files          /\n" +
                "#/       [how it works](https://dotenvx.com/encryption)     /\n" +
                "#/----------------------------------------------------------/\n" +
                $"DOTENV_PUBLIC_KEY=\"{publicKey}\"\n\n# .env\nHELLO=World\n\n");
        });
    }

    /// <remarks>Refer to "#decrypt --stdout - missing DOTENV_PRIVATE_KEY"</remarks>
    [Fact]
    public async Task DecryptStdoutMissingPrivateKey()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=World\n");
            await cli(["encrypt"]);
            File.Delete(Path.Combine(WorkDir, ".env.keys"));

            CliResult result = await cli(["decrypt", "--stdout"]);

            result.ExitCode.ShouldBe(1);
            result.StandardError.ShouldBe(DecryptFailed);
        });
    }

    private static Dictionary<string, string> NoSecretsEnv(params (string Key, string Value)[] extra)
    {
        return extra.ToDictionary(e => e.Key, e => e.Value);
    }

    /// <remarks>Refer to "#run - encrypted .env.production with no .env.keys, with DOTENV_PRIVATE_KEY_PRODUCTION"</remarks>
    [Fact]
    public async Task RunEncryptedEnvProductionWithNoEnvKeysWithPrivateKeyProduction()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env.production", "");
            await cli(["set", "HELLO", "production", "-f", ".env.production"]);
            WriteFile("index.js", "console.log('Hello ' + process.env.HELLO)\n");
            string privateKey = KeyFromKeysFile("DOTENV_PRIVATE_KEY_PRODUCTION");
            File.Delete(Path.Combine(WorkDir, ".env.keys")); // no keys file

            Dictionary<string, string> env = NoSecretsEnv(("DOTENV_PRIVATE_KEY_PRODUCTION", privateKey));

            Out(await cli(["run", "--", "node", "index.js"], env)).ShouldBe("Hello production");
            Out(await cli(["run", "--quiet", "--", "node", "index.js"], env)).ShouldBe("Hello production");
        });
    }

    private const string RedactScript =
        "process.stdout.write('stdout ' + process.env.SECRET + ' ' + process.env.PUBLIC + ' ' + process.env.INLINE + ' ' + process.env.VISIBLE_PLAIN); " +
        "process.stderr.write('stderr ' + process.env.SECRET + ' ' + process.env.PUBLIC + ' ' + process.env.INLINE + ' ' + process.env.VISIBLE_PLAIN)";

    /// <remarks>Refer to "#run - redacts injected values from stdout and stderr except _PLAIN values"</remarks>
    [Fact]
    public async Task RunRedactsInjectedValuesFromStdoutAndStderrExceptPlainValues()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "SECRET=super-secret-value\nPUBLIC=public-value\nVISIBLE_PLAIN=visible-value\n");
            WriteFile("index.js", RedactScript + "\n");

            // Windows defines a PUBLIC variable of its own (C:\Users\Public); pin it to the file value so the upstream expectations hold everywhere
            Dictionary<string, string> env = NoSecretsEnv(("PUBLIC", "public-value"));
            CliResult output = await cli(["run", "--quiet", "--redact", "--env", "INLINE=inline-value", "--", "node", "index.js"], env);

            output.StandardOutput.Trim().ShouldBe("stdout [REDACTED] [REDACTED] [REDACTED] visible-value");
            output.StandardError.Trim().ShouldBe("stderr [REDACTED] [REDACTED] [REDACTED] visible-value");

            CliResult unredacted = await cli(["run", "--quiet", "--env", "INLINE=inline-value", "--", "node", "index.js"], env);

            unredacted.StandardOutput.Trim().ShouldBe("stdout super-secret-value public-value inline-value visible-value");
            unredacted.StandardError.Trim().ShouldBe("stderr super-secret-value public-value inline-value visible-value");
        });
    }

    /// <remarks>Refer to "#run - redacts injected values repeated in command failure diagnostics"</remarks>
    [Fact]
    public async Task RunRedactsInjectedValuesRepeatedInCommandFailureDiagnostics()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "");
            await cli(["set", "SECRET", "super-secret-value"]);
            WriteFile("index.js", "process.stderr.write(process.env.SECRET); process.exit(1)\n");

            CliResult output = await cli(["run", "--quiet", "--redact", "--", "node", "index.js"]);

            output.StandardError.ShouldContain("[REDACTED]");
            output.StandardError.ShouldNotContain("super-secret-value");
            output.ExitCode.ShouldBe(1);
        });
    }

    /// <remarks>Refer to "#run - redacts pre-existing values when their keys are declared in .env"</remarks>
    [Fact]
    public async Task RunRedactsPreExistingValuesWhenTheirKeysAreDeclaredInEnv()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "SECRET=file-secret\nVISIBLE_PLAIN=file-visible\n");
            WriteFile("index.js", "process.stdout.write(process.env.SECRET + ' ' + process.env.VISIBLE_PLAIN)\n");
            Dictionary<string, string> env = NoSecretsEnv(("SECRET", "external-secret"), ("VISIBLE_PLAIN", "external-visible"));

            CliResult output = await cli(["run", "--quiet", "--redact", "--", "node", "index.js"], env);

            output.StandardOutput.Trim().ShouldBe("[REDACTED] external-visible");
            output.StandardOutput.ShouldNotContain("external-secret");

            CliResult unredacted = await cli(["run", "--quiet", "--", "node", "index.js"], env);

            unredacted.StandardOutput.Trim().ShouldBe("external-secret external-visible");
        });
    }

    /// <remarks>Refer to "#--version"</remarks>
    [Fact]
    public async Task Version()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string version = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixture.RepoRoot, "dotenvx", "package.json")))
            .RootElement.GetProperty("version").GetString()!;

        await ForBothAsync(async cli => Out(await cli(["--version"])).ShouldBe(version));
    }

    /// <remarks>Refer to "genexample works as a direct command"</remarks>
    [Fact]
    public async Task GenexampleWorksAsADirectCommand()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(".env", "HELLO=Dotenvx\n");

            CliResult result = await cli(["genexample"]);

            result.ExitCode.ShouldBe(0);
            (result.StandardOutput + result.StandardError).ShouldMatch(@"generated \(\.env\.example\)");
            ReadFile(".env.example").ShouldBe("# .env.example - generated with dotenvx\n\nHELLO=\n");
        });
    }

    /// <remarks>Refer to "precommit forwards a string directory to action" and "prebuild forwards a string directory to action"</remarks>
    [Theory]
    [InlineData("precommit", ".gitignore")]
    [InlineData("prebuild", ".dockerignore")]
    public async Task PrecommitAndPrebuildForwardAStringDirectoryToTheAction(string command, string ignoreFile)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        await ForBothAsync(async cli =>
        {
            WriteFile(ignoreFile, "");

            CliResult result = await cli([command]);

            result.ExitCode.ShouldBe(0);
            string output = result.StandardOutput + result.StandardError;
            output.ShouldNotContain("paths[0]");
            output.ShouldNotContain($"DEPRECATION NOTICE: [{command}]");
        });
    }
}
