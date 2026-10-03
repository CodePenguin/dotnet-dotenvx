using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/sets.test.js</remarks>
public class SetsTest : EnvironmentTestBase
{
    // mutateSrc.js: the public key header notes the keys file (default .env.keys in the working directory) relative to the env file
    private static string FkComment(string envFile)
    {
        string relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(envFile))!, Path.GetFullPath(".env.keys"));
        return relative == ".env.keys" ? "" : $" # -fk {relative}";
    }

    private static string? Pub(ProcessedSet row)
    {
        return Primitives.Primitives.PublicKeys(row.EnvSrc).FirstOrDefault();
    }

    private static string? Enc(ProcessedSet row)
    {
        return Primitives.Primitives.Scan(row.EnvSrc, new Primitives.ScanOptions())
            .Parsed.GetValueOrDefault(row.Key ?? "")?.LastOrDefault();
    }

    public SetsTest()
    {
        ClearEnvironment();
        Fsx.WriteFileX = (_, _) => { };
    }

    /// <remarks>
    /// Refer to "#run (no arguments)". Verified against the real Node CLI: a missing .env file is
    /// NOT an error by default for `set` either - it starts from an empty string (not encrypt's
    /// sample kit, since `set` is about to insert its own key/value pair) per
    /// dotenvx/src/lib/transforms/set.js's `if (!fileExists &amp;&amp; !noCreate)` branch. The
    /// original MISSING_ENV_FILE-error assertion moved to
    /// <see cref="TestRunNoArgumentsWithNoCreate"/>, which passes noCreate: true to reproduce it.
    /// </remarks>
    [Fact]
    public void TestRunNoArguments()
    {
        SetsResult result = new Sets(null, null, [], false).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBeNull();
        row.Value.ShouldBeNull();
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(".env"));
        row.EnvFilePath.ShouldBe(".env");
        row.Changed.ShouldBeTrue(); // transforms/set.js marks a to-be-created file as changed
        row.Error.ShouldBeNull();
        result.ChangedFilePaths.ShouldBe([".env"]);
    }

    /// <remarks>Refer to "#run (no arguments)", noCreate variant - see remarks on <see cref="TestRunNoArguments"/>.</remarks>
    [Fact]
    public void TestRunNoArgumentsWithNoCreate()
    {
        SetsResult result = new Sets(null, null, [], false, noCreate: true).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBeNull();
        row.Value.ShouldBeNull();
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(".env"));
        row.EnvFilePath.ShouldBe(".env");
        row.Changed.ShouldBeFalse();
        row.Error.ShouldBeOfType<MissingEnvFileException>();
        ((MissingEnvFileException)row.Error).Code.ShouldBe("MISSING_ENV_FILE");
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no env file)" - noCreate variant, see remarks on <see cref="TestRunNoArguments"/>.</remarks>
    [Fact]
    public void TestRunNoEnvFile()
    {
        SetsResult result = new Sets(null, null, [], false, noCreate: true).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBeNull();
        row.Value.ShouldBeNull();
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(".env"));
        row.EnvFilePath.ShouldBe(".env");
        row.Changed.ShouldBeFalse();
        row.Error.ShouldBeOfType<MissingEnvFileException>();
        ((MissingEnvFileException)row.Error).Code.ShouldBe("MISSING_ENV_FILE");
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>
    /// Refer to "#run (no arguments and some other error)". noCreate: true so the missing-file
    /// short-circuit doesn't bypass the mocked Fsx.ReadFileX before it gets a chance to throw -
    /// see remarks on <see cref="TestRunNoArguments"/>.
    /// </remarks>
    [Fact]
    public void TestRunNoArgumentsAndSomeOtherError()
    {
        Fsx.ReadFileX = (_, _) => throw new Exception("Mock Error");
        Fsx.ReadFileSync = _ => []; // Default to utf8 for DetectEncoding

        SetsResult result = new Sets(null, null, [], false, noCreate: true).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBeNull();
        row.Value.ShouldBeNull();
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(".env"));
        row.EnvFilePath.ShouldBe(".env");
        row.Changed.ShouldBeFalse();
        row.Error.ShouldNotBeNull();
        row.Error.Message.ShouldBe("Mock Error");
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (encrypt off) (finds .env file)"</remarks>
    [Fact]
    public void TestRunEncryptOffFindsEnvFile()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\nKEY=\"value\"\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, false).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    /// <remarks>Refer to "#run (encrypt off) (finds .env file and overwrites existing key/value)"</remarks>
    [Fact]
    public void TestRunEncryptOffFindsEnvFileAndOverwritesExistingKeyValue()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"new value\" # this is a comment\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, false).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (encrypt off) (finds .env file and attempts overwrite with same key/value)"</remarks>
    [Fact]
    public void TestRunEncryptOffFindsEnvFileAndAttemptsOverwriteWithSameKeyValue()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "frontend", envs, false).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("frontend");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeFalse();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (encrypt off) (finds .env file as array)"</remarks>
    [Fact]
    public void TestRunEncryptOffFindsEnvFileAsArray()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\nKEY=\"value\"\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, false).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (finds .env file) with --encrypt"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithEncrypt()
    {
        const string envFile = "tests/monorepo/apps/frontend/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        Enc(row).ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"{FkComment(envFile)}",
            "",
            "# .env",
            "# for testing purposes only",
            "HELLO=\"frontend\" # this is a comment",
            $"KEY=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (finds .env and .env.keys file) with --encrypt"</remarks>
    [Fact]
    public void TestRunFindsEnvAndEnvKeysFileWithEncrypt()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        Pub(row).ShouldNotBeNull();
        Enc(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"",
            "",
            "# .env",
            "HELLO=\"encrypted:BG8M6U+GKJGwpGA42ml2erb9+T2NBX6Z2JkBLynDy21poz0UfF5aPxCgRbIyhnQFdWKd0C9GZ7lM5PeL86xghoMcWvvPpkyQ0yaD2pZ64RzoxFGB1lTZYlEgQOxTDJnWxODHfuQcFY10uA==\"",
            $"KEY=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (finds .env and .env.keys file) with --encrypt and changes original value"</remarks>
    [Fact]
    public void TestRunFindsEnvAndEnvKeysFileWithEncryptAndChangesOriginalValue()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        Pub(row).ShouldNotBeNull();
        Enc(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"",
            "",
            "# .env",
            $"HELLO=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - no test with this title exists anywhere in
    /// the submodule at this pin. Rewritten 2026-10-02 after a code audit found and fixed a real bug
    /// in SmartDotenvPublicKey.cs: a stray DOTENV_PUBLIC_KEY in the process environment used to be
    /// checked BEFORE the target file's own public key, so it could look like a "mismatch" against
    /// an already-correctly-paired file. Live-verified against the real pinned Node CLI that the
    /// correct behavior is to ignore the process-env value entirely and encrypt normally using the
    /// file's own matching key.
    /// </remarks>
    [Fact]
    public void TestRunIgnoresStrayProcessEnvPublicKeyAndEncryptsNormally()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "12345");

        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        row.Error.ShouldBeNull();
        Enc(row).ShouldNotBeNull();
    }

    /// <remarks>Refer to "#run (finds .env file only) with --encrypt"</remarks>
    [Fact]
    public void TestRunFindsEnvFileOnlyWithEncrypt()
    {
        const string envKeysFilePath = "tests/monorepo/apps/encrypted/.env.keys";
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        string originalKeysFileContents = File.ReadAllText(envKeysFilePath);
        try
        {
            File.WriteAllText(envKeysFilePath, "");
            EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("HELLO", "new value", envs).Run();

            result.ProcessedEnvs.Length.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("HELLO");
            row.Value.ShouldBe("new value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath(envFile));
            row.EnvFilePath.ShouldBe(envFile);
            row.Changed.ShouldBeTrue();
            Pub(row).ShouldNotBeNull();
            Enc(row).ShouldNotBeNull();
            row.EnvSrc.ShouldBe(string.Join("\n", (string[])
            [
                "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
                "#/            public-key encryption for .env files          /",
                "#/       [how it works](https://dotenvx.com/encryption)     /",
                "#/----------------------------------------------------------/",
                $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"",
                "",
                "# .env",
                $"HELLO=\"{Enc(row)}\""
            ]) + "\n");
            result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
        }
        finally
        {
            File.WriteAllText(envKeysFilePath, originalKeysFileContents);
        }
    }

    /// <remarks>Refer to "#run (finds .env and .env.keys file but they are blank) with --encrypt"</remarks>
    [Fact]
    public void TestRunFindsEnvAndEnvKeysFileButTheyAreBlankWithEncrypt()
    {
        string envKeysFilePath = Path.Combine("tests", "monorepo", "apps", "encrypted", ".env.keys");
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        Fsx.ReadFileX = (_, _) => "";
        Fsx.ReadFileSync = _ => [];

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        result.KeysSrc.ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        Enc(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"{FkComment(envFile)}",
            "",
            "# .env",
            $"HELLO=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (finds .env and .env.keys file but they are not quite blank) with --encrypt"</remarks>
    [Fact]
    public void TestRunFindsEnvAndEnvKeysFileButTheyAreNotQuiteBlankWithEncrypt()
    {
        Fsx.ReadFileX = (filepath, _) => filepath.Contains(".env.keys") ? "" : "## hi";

        string envKeysFilePath = Path.Combine("tests", "monorepo", "apps", "encrypted", ".env.keys");
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        result.KeysSrc.ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        Enc(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"{FkComment(envFile)}",
            "",
            "# .env",
            "## hi",
            $"HELLO=\"{Enc(row)}\""
        ]));
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    /// <remarks>Refer to "#run (finds .env with a shebang) with --encrypt"</remarks>
    [Fact]
    public void TestRunFindsEnvWithAShebangWithEncrypt()
    {
        string envKeysFilePath = Path.Combine("tests", "monorepo", "apps", "shebang", ".env.keys");
        const string envFile = "tests/monorepo/apps/shebang/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        result.KeysSrc.ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        Enc(row).ShouldNotBeNull();
        row.EnvSrc.ShouldContain("#!/bin/bash");
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#!/bin/bash",
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"{FkComment(envFile)}",
            "",
            "# .env",
            $"HELLO=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/shebang/.env" });
    }

    /// <remarks>
    /// Refer to "#run (finds .env file only) with --encrypt AND setting from unencrypted to
    /// encrypted same value" - but the historical JS test of this name (retrieved via `git show
    /// 98f4373a^:tests/lib/services/sets.test.js`, the commit that deleted the pre-reorg
    /// `services/sets.js`/its test file) predates the `@dotenvx/primitives` refactor and asserted
    /// `changed: true` for this scenario. Live-verified against the ACTUAL pinned Node CLI
    /// (`tests/monorepo/apps/unencrypted/.env`'s real fixture, `HELLO="unencrypted"`, no existing
    /// key): `set HELLO unencrypted` prints "○ no change" and leaves `.env` byte-for-byte
    /// untouched, even though a `.env.keys` file IS still freshly created as a side effect (same
    /// unconditional-key-generation behavior already covered by the sibling shebang test above) -
    /// 2026-09-29, found investigating the Sets.cs re-encryption fix (this test previously locked
    /// in the exact bug that fix corrects, using an artificial `DOTENV_PUBLIC_KEY` process-env
    /// override that the real historical/current test never used, to dodge the nondeterminism of
    /// this scenario's genuinely fresh keypair).
    /// </remarks>
    [Fact]
    public void TestRunFindsEnvFileWithEncryptAndSettingFromUnencryptedToEncryptedSameValue()
    {
        string envKeysFilePath = Path.Combine("tests", "monorepo", "apps", "unencrypted", ".env.keys");
        const string envFile = "tests/monorepo/apps/unencrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult
            result = new Sets("HELLO", "unencrypted", envs)
                .Run(); // this value should be the same value as currently in the file

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("unencrypted");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeFalse();
        result.KeysSrc.ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        Enc(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\"{FkComment(envFile)}",
            "",
            "# .env",
            "HELLO=\"unencrypted\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (finds .env file) with --encrypt and custom envKeysFilepath"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithEncryptAndCustomEnvKeysFilePath()
    {
        const string envKeysFilePath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, true, envKeysFilePath).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        Enc(row).ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        result.KeysSrc.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{Pub(row)}\" # -fk {Path.Combine("..", "..", ".env.keys")}",
            "",
            "# .env",
            "# for testing purposes only",
            "HELLO=\"app1\"",
            $"KEY=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>Refer to "#run (finds .env file) with --encrypt and custom envKeysFilepath and privateKey already exists"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithEncryptAndCustomEnvKeysFilePathAndPrivateKeyAlreadyExists()
    {
        const string envKeysFilePath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, true, envKeysFilePath).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(envFile));
        row.EnvFilePath.ShouldBe(envFile);
        row.Changed.ShouldBeTrue();
        Enc(row).ShouldNotBeNull();
        Pub(row).ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY_PRODUCTION=\"{Pub(row)}\" # -fk {Path.Combine("..", "..", ".env.keys")}",
            "",
            "# .env.production",
            "# for testing purposes only",
            "HELLO=\"production\"",
            $"KEY=\"{Enc(row)}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-29 bug found via a code audit: set.js's
    /// `const noEncrypt = !options.encrypt || isPlainKey(key)` means a key ending in `_PLAIN` is
    /// always stored as plaintext regardless of the default `--encrypt`, and - since noEncrypt also
    /// gates the whole "generate a fresh keypair if none exists" branch upstream - no `.env.keys`
    /// file or `DOTENV_PUBLIC_KEY` header gets created either. Confirmed live against Node: `set
    /// HELLO_PLAIN mysecret` on a fresh `.env` with no existing key produces `HELLO_PLAIN="mysecret"`
    /// verbatim, no `.env.keys` side effect, and no `EnvKeysFilePath`/`PrivateKeyAdded` on the row.
    /// </remarks>
    [Fact]
    public void TestRunPlainSuffixedKeyStoresAsPlaintextEvenWithEncryptOn()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=World\n";
        Fsx.ReadFileSync = _ => [];
        Fsx.ExistsSync = filepath => filepath.EndsWith(".env");

        SetsResult result = new Sets("HELLO_PLAIN", "mysecret", []).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Changed.ShouldBeTrue();
        result.KeysSrc.ShouldBeNull();
        Pub(row).ShouldBeNull();
        Enc(row).ShouldBe("mysecret");
        row.EnvSrc.ShouldBe("HELLO=World\nHELLO_PLAIN=\"mysecret\"\n");
    }
}