using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/tests/e2e/run.test.js. The upstream repo has no
/// dotenvx/tests/lib/services/run.test.js at this pin - the only place Run's real end-to-end
/// behavior is exercised upstream is the e2e suite, which spawns real subprocesses. Per this
/// project's parity-suite convention, those e2e scenarios are ported here in-process against the
/// real Run service (mapping to upstream's src/lib/resolvers/envs.js) rather than by spawning a
/// child process.
/// </remarks>
public class RunTest : EnvironmentTestBase
{
    private const string EncryptedPrivateKey = "ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1";

    public RunTest()
    {
        // Same isolation approach as RunActionTest (the run command's own test class): keep PATH so
        // command substitution ($(cmd) -> cmd.exe /c) can resolve external commands like `whoami`.
        ClearEnvironmentExceptPath();
    }

    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRunBasic()
    {
        File.WriteAllText(".env", "HELLO=World\n");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RunProcessedEnv p = result.ProcessedEnvs[0];
        p.Type.ShouldBe(EnvType.EnvFile);
        p.Errors.ShouldBeEmpty();
        p.Parsed.ShouldBe(new Dictionary<string, string> { { "HELLO", "World" } });
        p.Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "World" } });

        result.UniqueInjectedKeys.ShouldBe(["HELLO"]);
        result.ReadableFilePaths.ShouldBe([".env"]);
        Environment.GetEnvironmentVariable("HELLO").ShouldBe("World");
        result.AfterEnv.Values.ShouldContain("World");
    }

    /// <remarks>Refer to "#run - multiple .env files"</remarks>
    [Fact]
    public void TestRunMultipleEnvFiles()
    {
        File.WriteAllText(".env.local", "HELLO=local\n");
        File.WriteAllText(".env", "HELLO=World\n");

        EnvItem[] envs =
        [
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        ];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.Length.ShouldBe(2);
        result.ProcessedEnvs[0].Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "local" } });
        result.ProcessedEnvs[0].PreExisted.ShouldBeEmpty();

        // Without --overload, the FIRST file's value wins - the second file's value pre-exists.
        result.ProcessedEnvs[1].PreExisted.ShouldBe(new Dictionary<string, string> { { "HELLO", "local" } });
        result.ProcessedEnvs[1].Injected.ShouldBeEmpty();

        result.UniqueInjectedKeys.ShouldBe(["HELLO"]);
        Environment.GetEnvironmentVariable("HELLO").ShouldBe("local");
    }

    /// <remarks>Refer to "#run - multiple .env files --overload"</remarks>
    [Fact]
    public void TestRunMultipleEnvFilesWithOverload()
    {
        File.WriteAllText(".env.local", "HELLO=local\n");
        File.WriteAllText(".env", "HELLO=World\n");

        EnvItem[] envs =
        [
            new() { Type = EnvType.EnvFile, Value = ".env.local" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        ];
        RunResult result = new Run(envs, overload: true).Execute();

        result.ProcessedEnvs.Length.ShouldBe(2);
        result.ProcessedEnvs[0].Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "local" } });
        result.ProcessedEnvs[0].PreExisted.ShouldBeEmpty();

        // With --overload, LATER files override earlier ones.
        result.ProcessedEnvs[1].Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "World" } });

        result.UniqueInjectedKeys.ShouldBe(["HELLO"]);
        Environment.GetEnvironmentVariable("HELLO").ShouldBe("World");
    }

    /// <remarks>Refer to "#run - Variable Expansion"</remarks>
    [Fact]
    public void TestRunVariableExpansion()
    {
        File.WriteAllText(".env",
            "USERNAME=\"username\"\nDATABASE_URL=\"postgres://${USERNAME}@localhost/my_database\"\n");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        result.ProcessedEnvs[0].Errors.ShouldBeEmpty();
        result.ProcessedEnvs[0].Parsed.ShouldBe(new Dictionary<string, string>
        {
            { "USERNAME", "username" },
            { "DATABASE_URL", "postgres://username@localhost/my_database" }
        });
        Environment.GetEnvironmentVariable("DATABASE_URL").ShouldBe("postgres://username@localhost/my_database");
    }

    /// <remarks>
    /// Refer to "#run - Command Substitution". The exact substituted value depends on the host
    /// OS's `whoami` output, so assert the substitution happened at all rather than hardcoding a
    /// username.
    /// </remarks>
    [Fact]
    public void TestRunCommandSubstitution()
    {
        File.WriteAllText(".env", "DATABASE_URL=\"postgres://$(whoami)@localhost/my_database\"\n");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        result.ProcessedEnvs[0].Errors.ShouldBeEmpty();
        string value = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? throw new InvalidOperationException("DATABASE_URL was not injected into the environment");
        value.ShouldNotContain("$(whoami)");
        value.ShouldStartWith("postgres://");
        value.ShouldEndWith("@localhost/my_database");
    }

    /// <remarks>Refer to "#run - --env"</remarks>
    [Fact]
    public void TestRunEnvFlag()
    {
        File.WriteAllText(".env", "HELLO=World\n");

        EnvItem[] envs =
        [
            new() { Type = EnvType.Env, Value = "HELLO=String" },
            new() { Type = EnvType.EnvFile, Value = ".env" }
        ];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.Length.ShouldBe(2);
        result.ProcessedEnvs[0].Type.ShouldBe(EnvType.Env);
        result.ProcessedEnvs[0].Injected.ShouldBe(new Dictionary<string, string> { { "HELLO", "String" } });
        result.ProcessedEnvs[1].PreExisted.ShouldBe(new Dictionary<string, string> { { "HELLO", "String" } });
        result.ProcessedEnvs[1].Injected.ShouldBeEmpty();

        // The --env string is processed first, so its value wins over the file's.
        result.UniqueInjectedKeys.ShouldBe(["HELLO"]);
        result.ReadableStrings.ShouldBe(["HELLO=String"]);
        Environment.GetEnvironmentVariable("HELLO").ShouldBe("String");
    }

    /// <remarks>Refer to "#run - encrypted .env"</remarks>
    [Fact]
    public void TestRunEncryptedEnvFile()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFilePath = "tests/monorepo/apps/encrypted/.env.keys";

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        RunResult result = new Run(envs, envKeysFilePath: envKeysFilePath).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RunProcessedEnv p = result.ProcessedEnvs[0];
        p.Errors.ShouldBeEmpty();
        p.PrivateKey.ShouldBe(EncryptedPrivateKey);
        p.PrivateKeyName.ShouldBe("DOTENV_PRIVATE_KEY");
        p.Parsed["HELLO"].ShouldBe("encrypted");
        Environment.GetEnvironmentVariable("HELLO").ShouldBe("encrypted");
        result.UniqueInjectedKeys.ShouldContain("HELLO");
        result.UniqueInjectedKeys.ShouldContain("DOTENV_PUBLIC_KEY");
    }

    /// <remarks>Refer to "#run - encrypted .env with no .env.keys, with DOTENV_PRIVATE_KEY"</remarks>
    [Fact]
    public void TestRunEncryptedEnvFileWithPrivateKeyEnvVar()
    {
        // Stand-in for the e2e test's `rm .env.keys`: the private key must come from the
        // DOTENV_PRIVATE_KEY environment variable, not from a .env.keys file next to the .env.
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env", overwrite: true);
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", EncryptedPrivateKey);

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        result.ProcessedEnvs[0].Errors.ShouldBeEmpty();
        result.ProcessedEnvs[0].Parsed["HELLO"].ShouldBe("encrypted");
        Environment.GetEnvironmentVariable("HELLO").ShouldBe("encrypted");
        result.UniqueInjectedKeys.ShouldContain("HELLO");
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-25 fix. Confirmed live that
    /// @dotenvx/primitives' parse() (what resolvers/envs.js, i.e. this class, actually decrypts
    /// through) coarsens EVERY decrypt failure to generic DECRYPTION_FAILED, never the specific
    /// WRONG_PRIVATE_KEY/MISSING_PRIVATE_KEY/etc. codes - those are only real for main.js's own
    /// standalone parse() function (Main.Parse() here), which manually calls decryptKeyValue()
    /// per key instead of going through the resolver. Run.cs previously surfaced the specific
    /// WrongPrivateKeyException directly (correct for Main.Parse(), wrong for everything routing
    /// through Run - CLI run/get/validate, and Main.Config()/Main.Get()).
    /// </remarks>
    [Fact]
    public void TestRunWrongPrivateKeyCoarsensToGenericDecryptionFailed()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env", overwrite: true);
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY",
            "0000000000000000000000000000000000000000000000000000000000000001");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        Exception error = result.ProcessedEnvs[0].Errors.ShouldHaveSingleItem();
        DotenvxException dotenvxError = error.ShouldBeOfType<DecryptionFailedException>();
        dotenvxError.Code.ShouldBe("DECRYPTION_FAILED");
        dotenvxError.Message.ShouldBe("[DECRYPTION_FAILED] could not decrypt HELLO");
    }

    /// <remarks>
    /// Not from a JS test - companion to the WRONG_PRIVATE_KEY regression test above, confirming
    /// a fully missing private key coarsens the same way (also confirmed live: parse() returns
    /// DECRYPTION_FAILED, not MISSING_PRIVATE_KEY, when no key is available at all).
    /// </remarks>
    [Fact]
    public void TestRunMissingPrivateKeyCoarsensToGenericDecryptionFailed()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env", overwrite: true);

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RunResult result = new Run(envs).Execute();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        Exception error = result.ProcessedEnvs[0].Errors.ShouldHaveSingleItem();
        error.ShouldBeOfType<DecryptionFailedException>();
    }

    /// <remarks>Refer to "#run - env var precedence: environment variables take precedence over .env files by default"</remarks>
    [Fact]
    public void TestRunEnvVarPrecedenceOverEnvFiles()
    {
        File.WriteAllText(".env.prod", "MODEL_REGISTRY=registry.company.com/models/v1\n");
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env.prod" }];

        // No pre-existing value in the env - the .env.prod file's value should be used.
        Environment.SetEnvironmentVariable("MODEL_REGISTRY", null);
        RunResult result = new Run(envs).Execute();
        result.ProcessedEnvs[0].Errors.ShouldBeEmpty();
        Environment.GetEnvironmentVariable("MODEL_REGISTRY").ShouldBe("registry.company.com/models/v1");

        // Pre-existing env var + no overload - the pre-existing value wins; the file does NOT override.
        Environment.SetEnvironmentVariable("MODEL_REGISTRY", "registry.azure.com/models/v2");
        result = new Run(envs).Execute();
        result.ProcessedEnvs[0].Errors.ShouldBeEmpty();
        result.ProcessedEnvs[0].PreExisted
            .ShouldBe(new Dictionary<string, string> { { "MODEL_REGISTRY", "registry.azure.com/models/v2" } });
        Environment.GetEnvironmentVariable("MODEL_REGISTRY").ShouldBe("registry.azure.com/models/v2");

        // Same pre-existing value + overload - the file's value wins this time.
        result = new Run(envs, overload: true).Execute();
        result.ProcessedEnvs[0].Errors.ShouldBeEmpty();
        Environment.GetEnvironmentVariable("MODEL_REGISTRY").ShouldBe("registry.company.com/models/v1");
    }
}