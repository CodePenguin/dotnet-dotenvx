using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Services;

public class SetsTest : EnvironmentTestBase
{
    public SetsTest()
    {
        ClearEnvironment();
    }

    [Fact]
    public void RunNoArguments()
    {
        SetsResult result = new Sets(null, null, [], false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
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

    [Fact]
    public void RunNoEnvFile()
    {
        SetsResult result = new Sets(null, null, [], false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
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

    [Fact]
    public void RunNoArgumentsAndSomeOtherError()
    {
        SetsResult result = new Sets(null, null, new List<EnvItem>(), false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBeNull();
        row.Value.ShouldBeNull();
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath(".env"));
        row.EnvFilePath.ShouldBe(".env");
        row.Changed.ShouldBeFalse();
        row.Error.ShouldNotBeNull();
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    [Fact]
    public void RunEncryptOffFindsEnvFile()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\nKEY=\"value\"\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/frontend/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/frontend/.env");
        row.Changed.ShouldBeTrue();
        row.OriginalValue.ShouldBeNull();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    [Fact]
    public void RunEncryptOffFindsEnvFileAndOverwritesExistingKeyValue()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"new value\" # this is a comment\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/frontend/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/frontend/.env");
        row.OriginalValue.ShouldBe("frontend");
        row.Changed.ShouldBeTrue();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    [Fact]
    public void RunEncryptOffFindsEnvFileAndAttemptsOverwriteWithSameKeyValue()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "frontend", envs, false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("frontend");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/frontend/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/frontend/.env");
        row.Changed.ShouldBeFalse();
        row.OriginalValue.ShouldBe("frontend");
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    [Fact]
    public void RunEncryptOffFindsEnvFileAsArray()
    {
        const string envSrc = "# for testing purposes only\nHELLO=\"frontend\" # this is a comment\nKEY=\"value\"\n";

        const string envFile = "tests/monorepo/apps/frontend/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, false).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/frontend/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/frontend/.env");
        row.Changed.ShouldBeTrue();
        row.OriginalValue.ShouldBeNull();
        row.EnvSrc.ShouldBe(envSrc);
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    [Fact]
    public void RunFindsEnvFileWithEncrypt()
    {
        const string envFile = "tests/monorepo/apps/frontend/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/frontend/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/frontend/.env");
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
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileWithEncrypt()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("KEY");
        row.Value.ShouldBe("value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/encrypted/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env");
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
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileWithEncryptAndChangesOriginalValue()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/encrypted/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env");
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
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileWithEncryptButDerivedPublicKeyDoesNotMatchConfiguredPublicKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "12345");

        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs).Run();

        result.ProcessedEnvs.Count.ShouldBe(1);
        ProcessedSet row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("HELLO");
        row.Value.ShouldBe("new value");
        row.Type.ShouldBe(EnvType.EnvFile);
        row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/encrypted/.env"));
        row.EnvFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env");
        row.Changed.ShouldBeFalse();
        row.OriginalValue.ShouldBe("encrypted");
        row.Error.ShouldBeOfType<InvalidDotenvPrivateKeyException>();
        ((InvalidDotenvPrivateKeyException)row.Error).Code.ShouldBe("INVALID_DOTENV_PRIVATE_KEY");
    }

    [Fact]
    public void RunFindsEnvFileOnlyWithEncrypt()
    {
        const string envKeysPath = "tests/monorepo/apps/encrypted/.env.keys";
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        string originalKeys = File.ReadAllText(envKeysPath);
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            File.WriteAllText(envKeysPath, "");
            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("HELLO", "new value", envs).Run();

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("HELLO");
            row.Value.ShouldBe("new value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/encrypted/.env"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env");
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
            result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
        }
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
            File.WriteAllText(envKeysPath, originalKeys);
        }
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileButTheyAreBlankWithEncrypt()
    {
        const string envKeysPath = "tests/monorepo/apps/encrypted/.env.keys";
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        string originalKeys = File.ReadAllText(envKeysPath);
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            // Blank .env and .env.keys
            File.WriteAllText(envKeysPath, "");
            File.WriteAllText(envFile, "");
            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("HELLO", "new value", envs).Run();

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("HELLO");
            row.Value.ShouldBe("new value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/encrypted/.env"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env");
            row.Changed.ShouldBeTrue();
            row.OriginalValue.ShouldBeNull();
            row.PrivateKeyAdded.ShouldBeTrue();
            row.EnvKeysFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env.keys");
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
            result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
        }
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
            File.WriteAllText(envKeysPath, originalKeys);
        }
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileButTheyAreNotQuiteBlankWithEncrypt()
    {
        const string envKeysPath = "tests/monorepo/apps/encrypted/.env.keys";
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        string originalKeys = File.ReadAllText(envKeysPath);
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            // Blank .env.keys and not quite blank .env
            File.WriteAllText(envKeysPath, "");
            File.WriteAllText(envFile, "## hi");
            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("HELLO", "new value", envs).Run();

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("HELLO");
            row.Value.ShouldBe("new value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/encrypted/.env"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env");
            row.Changed.ShouldBeTrue();
            row.OriginalValue.ShouldBeNull();
            row.PrivateKeyAdded.ShouldBeTrue();
            row.EnvKeysFilePath.ShouldBe("tests/monorepo/apps/encrypted/.env.keys");
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
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
            File.WriteAllText(envKeysPath, originalKeys);
        }
    }

    [Fact]
    public void RunFindsEnvWithAShebangWithEncrypt()
    {
        const string envKeysPath = "tests/monorepo/apps/shebang/.env.keys";
        const string envFile = "tests/monorepo/apps/shebang/.env";
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("HELLO", "new value", envs).Run();

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("HELLO");
            row.Value.ShouldBe("new value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/shebang/.env"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/shebang/.env");
            row.EnvKeysFilePath.ShouldBe("tests/monorepo/apps/shebang/.env.keys");
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
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
            File.Delete(envKeysPath);
        }
    }

    [Fact]
    public void RunFindsEnvFileWithEncryptAndSettingFromUnencryptedToEncryptedSameValue()
    {
        const string envFile = "tests/monorepo/apps/unencrypted/.env";
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY",
                "03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba");

            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult
                result = new Sets("HELLO", "unencrypted", envs)
                    .Run(); // this value should be the same value as currently in the file

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("HELLO");
            row.Value.ShouldBe("unencrypted");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/unencrypted/.env"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/unencrypted/.env");
            row.Changed.ShouldBeTrue();
            row.OriginalValue.ShouldBe("unencrypted");
            row.PublicKey.ShouldNotBeNull();
            row.PrivateKey.ShouldBeNull();
            row.EncryptedValue.ShouldNotBeNull();
            row.PrivateKeyName.ShouldNotBeNull();
            row.EnvSrc.ShouldBe($"HELLO=\"{row.EncryptedValue}\"\n");
            result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/unencrypted/.env" });
        }
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
        }
    }

    [Fact]
    public void RunFindsEnvFileWithEncryptAndCustomEnvKeysFilepath()
    {
        const string envKeysFilepath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env";
        string originalKeys = File.ReadAllText(envKeysFilepath);
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("KEY", "value", envs, true, envKeysFilepath).Run();

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("KEY");
            row.Value.ShouldBe("value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/app1/.env"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/app1/.env");
            row.Changed.ShouldBeTrue();
            row.OriginalValue.ShouldBeNull();
            row.EncryptedValue.ShouldNotBeNull();
            row.PublicKey.ShouldNotBeNull();
            row.PrivateKey.ShouldNotBeNull();
            row.EnvKeysFilePath.ShouldBe("tests/monorepo/.env.keys");
            row.PrivateKeyAdded.ShouldBeTrue();
            row.PrivateKeyName.ShouldNotBeNull();
            row.EnvSrc.ShouldBe(string.Join("\n", (string[])
            [
                "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
                "#/            public-key encryption for .env files          /",
                "#/       [how it works](https://dotenvx.com/encryption)     /",
                "#/----------------------------------------------------------/",
                $"DOTENV_PUBLIC_KEY=\"{row.PublicKey}\" # -fk ../../.env.keys",
                "",
                "# .env",
                "# for testing purposes only",
                "HELLO=\"app1\"",
                $"KEY=\"{row.EncryptedValue}\""
            ]) + "\n");
            result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/app1/.env" });
        }
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
            File.WriteAllText(envKeysFilepath, originalKeys);
        }
    }

    [Fact]
    public void RunFindsEnvFileWithEncryptAndCustomEnvKeysFilepathAndPrivateKeyAlreadyExists()
    {
        const string envKeysFilepath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env.production";
        string originalFileContents = File.ReadAllText(envFile);
        try
        {
            List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

            SetsResult result = new Sets("KEY", "value", envs, true, envKeysFilepath).Run();

            result.ProcessedEnvs.Count.ShouldBe(1);
            ProcessedSet row = result.ProcessedEnvs[0];
            row.Key.ShouldBe("KEY");
            row.Value.ShouldBe("value");
            row.Type.ShouldBe(EnvType.EnvFile);
            row.FilePath.ShouldBe(Path.GetFullPath("tests/monorepo/apps/app1/.env.production"));
            row.EnvFilePath.ShouldBe("tests/monorepo/apps/app1/.env.production");
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
                $"DOTENV_PUBLIC_KEY_PRODUCTION=\"{row.PublicKey}\" # -fk ../../.env.keys",
                "",
                "# .env.production",
                "# for testing purposes only",
                "HELLO=\"production\"",
                $"KEY=\"{row.EncryptedValue}\""
            ]) + "\n");
            result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/app1/.env.production" });
        }
        finally
        {
            File.WriteAllText(envFile, originalFileContents);
        }
    }
}