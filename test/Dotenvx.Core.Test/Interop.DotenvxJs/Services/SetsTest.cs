using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/sets.test.js</remarks>
public class SetsTest : EnvironmentTestBase
{
    public SetsTest()
    {
        ClearEnvironment();
        Fsx.WriteFileX = (_, _) => { };
    }

    /// <remarks>Refer to "#run (no arguments)"</remarks>
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
        row.Changed.ShouldBeFalse();
        row.Error.ShouldBeOfType<MissingEnvFileException>();
        ((MissingEnvFileException)row.Error).Code.ShouldBe("MISSING_ENV_FILE");
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no env file)"</remarks>
    [Fact]
    public void TestRunNoEnvFile()
    {
        SetsResult result = new Sets(null, null, [], false).Run();

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

    /// <remarks>Refer to "#run (no arguments and some other error)"</remarks>
    [Fact]
    public void TestRunNoArgumentsAndSomeOtherError()
    {
        Fsx.ReadFileX = (_, _) => throw new Exception("Mock Error");
        Fsx.ReadFileSync = _ => []; // Default to utf8 for DetectEncoding

        SetsResult result = new Sets(null, null, [], false).Run();

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
        row.OriginalValue.ShouldBeNull();
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
        row.OriginalValue.ShouldBe("frontend");
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
        row.OriginalValue.ShouldBe("frontend");
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
        row.OriginalValue.ShouldBeNull();
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
        row.OriginalValue.ShouldBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
            "",
            "# .env",
            "# for testing purposes only",
            "HELLO=\"frontend\" # this is a comment",
            $"KEY=\"{row.EncryptedValue}\""
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
        row.OriginalValue.ShouldBeNull();
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
            "",
            "# .env",
            "HELLO=\"encrypted:BG8M6U+GKJGwpGA42ml2erb9+T2NBX6Z2JkBLynDy21poz0UfF5aPxCgRbIyhnQFdWKd0C9GZ7lM5PeL86xghoMcWvvPpkyQ0yaD2pZ64RzoxFGB1lTZYlEgQOxTDJnWxODHfuQcFY10uA==\"",
            $"KEY=\"{row.EncryptedValue}\""
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
        row.OriginalValue.ShouldBe("encrypted");
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
            "",
            "# .env",
            $"HELLO=\"{row.EncryptedValue}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }

    /// <remarks>
    ///     Refer to "#run (finds .env and .env.keys file) with --encrypt but derived public key does not match configured
    ///     public key"
    /// </remarks>
    [Fact]
    public void TestRunFindsEnvAndEnvKeysFileWithEncryptButDerivedPublicKeyDoesNotMatchConfiguredPublicKey()
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
        row.Changed.ShouldBeFalse();
        row.OriginalValue.ShouldBe("encrypted");
        row.Error.ShouldBeOfType<InvalidDotenvPrivateKeyException>();
        ((InvalidDotenvPrivateKeyException)row.Error).Code.ShouldBe("INVALID_DOTENV_PRIVATE_KEY");
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
            row.OriginalValue.ShouldBe(
                "encrypted:BG8M6U+GKJGwpGA42ml2erb9+T2NBX6Z2JkBLynDy21poz0UfF5aPxCgRbIyhnQFdWKd0C9GZ7lM5PeL86xghoMcWvvPpkyQ0yaD2pZ64RzoxFGB1lTZYlEgQOxTDJnWxODHfuQcFY10uA==");
            row.PublicKey.ShouldNotBeNull();
            row.PrivateKey.ShouldBeNull();
            row.EncryptedValue.ShouldNotBeNull();
            row.PrivateKeyName.ShouldBe("DOTENV_PRIVATE_KEY");
            row.EnvSrc.ShouldBe(string.Join("\n", (string[])
            [
                "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
                "#/            public-key encryption for .env files          /",
                "#/       [how it works](https://dotenvx.com/encryption)     /",
                "#/----------------------------------------------------------/",
                $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
                "",
                "# .env",
                $"HELLO=\"{row.EncryptedValue}\""
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
        row.OriginalValue.ShouldBeNull();
        row.PrivateKeyAdded.ShouldBeTrue();
        row.EnvKeysFilePath.ShouldBe(envKeysFilePath);
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
            "",
            "# .env",
            $"HELLO=\"{row.EncryptedValue}\""
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
        row.OriginalValue.ShouldBeNull();
        row.PrivateKeyAdded.ShouldBeTrue();
        row.EnvKeysFilePath.ShouldBe(envKeysFilePath);
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
            "",
            "# .env",
            "## hi",
            $"HELLO=\"{row.EncryptedValue}\""
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
        row.EnvKeysFilePath.ShouldBe(envKeysFilePath);
        row.Changed.ShouldBeTrue();
        row.OriginalValue.ShouldBe("shebang");
        row.PrivateKeyAdded.ShouldBeTrue();
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldContain("#!/bin/bash");
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#!/bin/bash",
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\"",
            "",
            "# .env",
            $"HELLO=\"{row.EncryptedValue}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/shebang/.env" });
    }

    /// <remarks>Refer to "#run (finds .env file only) with --encrypt AND setting from unencrypted to encrypted same value"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithEncryptAndSettingFromUnencryptedToEncryptedSameValue()
    {
        const string envFile = "tests/monorepo/apps/unencrypted/.env";
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY",
            "03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba");

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
        row.Changed.ShouldBeTrue();
        row.OriginalValue.ShouldBe("unencrypted");
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe($"HELLO=\"{row.EncryptedValue}\"\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
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
        row.OriginalValue.ShouldBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.EnvKeysFilePath.ShouldBe(envKeysFilePath);
        row.PrivateKeyAdded.ShouldBeTrue();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\" # -fk {Path.Combine("..", "..", ".env.keys")}",
            "",
            "# .env",
            "# for testing purposes only",
            "HELLO=\"app1\"",
            $"KEY=\"{row.EncryptedValue}\""
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
        row.OriginalValue.ShouldBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PublicKey.ShouldNotBeNull();
        row.PrivateKey.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe(string.Join("\n", (string[])
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"DOTENV_PUBLIC_KEY_PRODUCTION=\"{row.PublicKey}\" # -fk {Path.Combine("..", "..", ".env.keys")}",
            "",
            "# .env.production",
            "# for testing purposes only",
            "HELLO=\"production\"",
            $"KEY=\"{row.EncryptedValue}\""
        ]) + "\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { envFile });
    }
}