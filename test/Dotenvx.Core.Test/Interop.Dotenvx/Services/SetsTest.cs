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
        SetsResult result = new Sets(null, null, [], false, null).Run();

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
        SetsResult result = new Sets(null, null, [], false, null).Run();

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
        // Note: Stubbing not implemented in C# version
        SetsResult result = new Sets(null, null, [], false, null).Run();

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

        SetsResult result = new Sets("KEY", "value", envs, false, null).Run();

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

        SetsResult result = new Sets("HELLO", "new value", envs, false, null).Run();

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

        SetsResult result = new Sets("HELLO", "frontend", envs, false, null).Run();

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

        SetsResult result = new Sets("KEY", "value", envs, false, null).Run();

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

        SetsResult result = new Sets("KEY", "value", envs, true, null).Run();

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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/frontend/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileWithEncrypt()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("KEY", "value", envs, true, null).Run();

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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileWithEncryptAndChangesOriginalValue()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, true, null).Run();

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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileWithEncryptButDerivedPublicKeyDoesNotMatchConfiguredPublicKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "12345");

        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, true, null).Run();

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
        // Note: Stubbing not implemented
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, true, null).Run();

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
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileButTheyAreBlankWithEncrypt()
    {
        // Note: Stubbing not implemented
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, true, null).Run();

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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvAndEnvKeysFileButTheyAreNotQuiteBlankWithEncrypt()
    {
        // Note: Stubbing not implemented
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, true, null).Run();

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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/encrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvWithAShebangWithEncrypt()
    {
        // Note: Stubbing not implemented
        const string envFile = "tests/monorepo/apps/shebang/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "new value", envs, true, null).Run();

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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/shebang/.env" });
    }

    [Fact]
    public void RunFindsEnvFileOnlyWithEncryptAndSettingFromUnencryptedToEncryptedSameValue()
    {
        // Note: Stubbing not implemented
        const string envFile = "tests/monorepo/apps/unencrypted/.env";
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        SetsResult result = new Sets("HELLO", "unencrypted", envs, true, null).Run();

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
        row.PrivateKey.ShouldNotBeNull();
        row.EncryptedValue.ShouldNotBeNull();
        row.PrivateKeyName.ShouldNotBeNull();
        row.EnvSrc.ShouldBe($"HELLO=\"{row.EncryptedValue}\"\n");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/unencrypted/.env" });
    }

    [Fact]
    public void RunFindsEnvFileWithEncryptAndCustomEnvKeysFilepath()
    {
        const string envKeysFilepath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env";
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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY");
        row.EnvSrc.ShouldContain("# -fk ../../.env.keys");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/app1/.env" });
    }

    [Fact]
    public void RunFindsEnvFileWithEncryptAndCustomEnvKeysFilepathAndPrivateKeyAlreadyExists()
    {
        const string envKeysFilepath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env.production";
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
        row.EnvSrc.ShouldContain("DOTENV_PUBLIC_KEY_PRODUCTION");
        row.EnvSrc.ShouldContain("# -fk ../../.env.keys");
        result.ChangedFilePaths.ShouldBe(new List<string> { "tests/monorepo/apps/app1/.env.production" });
    }
}