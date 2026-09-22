using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/encrypt.test.js</remarks>
public class EncryptTest : EnvironmentTestBase
{
    public EncryptTest()
    {
        ClearEnvironment();
    }

    protected override void TearDown()
    {
        File.Delete(".env");
        File.Delete(".env.keys");
        base.TearDown();
    }

    /// <remarks>
    /// Refer to "#run (no arguments)". Verified against the real Node CLI: a missing .env file is
    /// NOT an error by default - it gets replaced with the sample kit (HELLO="World") and
    /// encrypted, per dotenvx/src/lib/transforms/encrypt.js's `if (!fileExists &amp;&amp; !noCreate)`
    /// branch. The original MISSING_ENV_FILE-error assertion moved to
    /// <see cref="TestRunNoArgumentsWithNoCreate"/>, which passes noCreate: true to reproduce it.
    /// </remarks>
    [Fact]
    public void TestRunNoArguments()
    {
        EncryptResult result = new Encrypt([], null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeNull();

        result.ChangedFilePaths.ShouldBe([".env"]);
        result.UnchangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no arguments)", noCreate variant - see remarks on <see cref="TestRunNoArguments"/>.</remarks>
    [Fact]
    public void TestRunNoArgumentsWithNoCreate()
    {
        EncryptResult result = new Encrypt([], null, null, null, noCreate: true).Run();

        MissingEnvFileException exampleError = new(".env");

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no env file)" - noCreate variant, see remarks on <see cref="TestRunNoArguments"/>.</remarks>
    [Fact]
    public void TestRunNoEnvFile()
    {
        EncryptResult result = new Encrypt([], null, null, null, noCreate: true).Run();

        MissingEnvFileException exampleError = new(".env");

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBeEmpty();
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

        EncryptResult result = new Encrypt([], null, null, null, noCreate: true).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Error.ShouldBeOfType<Exception>();
        p.Error.Message.ShouldBe("Mock Error");
        p.EnvFilePath.ShouldBe(".env");
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (finds .env file)"</remarks>
    [Fact]
    public void TestRunFindsEnvFile()
    {
        const string envFile = "tests/monorepo/apps/frontend/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
    }

    /// <remarks>Refer to "#run (finds .env file with multiline values - implicit and explicit newline)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithMultilineValuesImplicitAndExplicitNewline()
    {
        const string envFile = "tests/monorepo/apps/multiline/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "ALOHA"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "ALOHA"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["ALOHA"] as string).ShouldStartWith("encrypted:");

        p.EnvSrc.ShouldBe(string.Concat(
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/\n",
            "#/            public-key encryption for .env files          /\n",
            "#/       [how it works](https://dotenvx.com/encryption)     /\n",
            "#/----------------------------------------------------------/\n",
            $"DOTENV_PUBLIC_KEY=\"{parsed["DOTENV_PUBLIC_KEY"]}\"\n\n",
            "# .env\n",
            $"HELLO=\"{parsed["HELLO"]}\"\n",
            $"ALOHA=\"{parsed["ALOHA"]}\"\n"
        ));
    }

    /// <remarks>Refer to "#run (finds .env file with CRLF multiline values - implicit and explicit CRLF newline)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithCrlfMultilineValuesImplicitAndExplicitCrlfNewline()
    {
        const string envFile = "tests/monorepo/apps/multiline/.env.crlf";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "ALOHA"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_CRLF", "HELLO", "ALOHA"]);
        (parsed["DOTENV_PUBLIC_KEY_CRLF"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["ALOHA"] as string).ShouldStartWith("encrypted:");

        p.EnvSrc.ShouldBe(string.Concat(
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/\n",
            "#/            public-key encryption for .env files          /\n",
            "#/       [how it works](https://dotenvx.com/encryption)     /\n",
            "#/----------------------------------------------------------/\n",
            $"DOTENV_PUBLIC_KEY_CRLF=\"{parsed["DOTENV_PUBLIC_KEY_CRLF"]}\"\n\n",
            "# .env.crlf\n",
            $"HELLO=\"{parsed["HELLO"]}\"\r\n",
            $"ALOHA=\"{parsed["ALOHA"]}\"\r\n"
        ));
    }

    [Fact]
    public void TestRunFindsEnvFileAlreadyEncrypted()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([envFile]);
    }

    /// <remarks>Refer to "#run (finds .env file with specified key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, ["HELLO2"], null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
    }

    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, ["HELLO2"], null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
    }

    /// <remarks>Refer to "#run (finds .env file with specified glob string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedGlobString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, ["H*"], null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO2", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldStartWith("encrypted:");
    }

    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, ["HELLO2"], null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldBe("two");
        (parsed["HELLO3"] as string).ShouldStartWith("encrypted:");
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key as string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, ["HELLO3"], null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldBe("three");
    }

    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKeyGlobbed()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, ["HE*"], null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([envFile]);

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldBe("two");
        (parsed["HELLO3"] as string).ShouldBe("three");
    }

    /// <remarks>Refer to "#run (finds .env.export file with exported key)"</remarks>
    [Fact]
    public void TestRunFindsEnvExportFileWithExportedKey()
    {
        const string envFile = "tests/.env.export";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["KEY"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_EXPORT", "KEY"]);
        (parsed["DOTENV_PUBLIC_KEY_EXPORT"] as string).ShouldNotBeNullOrEmpty();
        (parsed["KEY"] as string).ShouldStartWith("encrypted:");

        p.EnvSrc.ShouldBe(
            $"""
             #!/usr/bin/env bash
             #/-------------------[DOTENV_PUBLIC_KEY]--------------------/
             #/            public-key encryption for .env files          /
             #/       [how it works](https://dotenvx.com/encryption)     /
             #/----------------------------------------------------------/
             DOTENV_PUBLIC_KEY_EXPORT="{parsed["DOTENV_PUBLIC_KEY_EXPORT"] as string}"

             # .env.export
             export KEY={parsed["KEY"] as string}

             """);
    }

    /// <remarks>Refer to "#run (finds .env and .env.keys file) but derived public key does not match configured public key"</remarks>
    [Fact]
    public void TestRunFindsEnvAndEnvKeysFileButDerivedPublicKeyDoesNotMatchConfiguredPublicKey()
    {
        Environment.SetEnvironmentVariable("DOTENV_PUBLIC_KEY", "12345");

        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Error.ShouldBeOfType<MispairedPrivateKeyException>();
    }

    /// <remarks>Refer to "#run (finds .env file only)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileOnly()
    {
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        string originalKeysFileContents = File.ReadAllText(envKeysFile);
        try
        {
            // Delete the .env.keys file so only the .env file is found
            File.Delete(envKeysFile);
            EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
            EncryptResult result = new Encrypt(envs, null, null, null).Run();

            result.ProcessedEnvs.ShouldHaveSingleItem();
            EncryptProcessedEnv p = result.ProcessedEnvs[0];
            p.Keys.ShouldBeEmpty();
            p.EnvFilePath.ShouldBe(envFile);
            result.UnchangedFilePaths.ShouldBe([envFile]);
        }
        finally
        {
            File.WriteAllText(envKeysFile, originalKeysFileContents);
        }
    }

    /// <remarks>Refer to "#run (finds .env file) and custom envKeysFilepath"</remarks>
    [Fact]
    public void TestRunFindsEnvFileAndCustomEnvKeysFilePath()
    {
        const string envKeysFilePath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env";
        string originalKeysFileContents = File.ReadAllText(envKeysFilePath);
        try
        {
            EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
            EncryptResult result = new Encrypt(envs, null, null, envKeysFilePath).Run();

            result.ProcessedEnvs.ShouldHaveSingleItem();
            EncryptProcessedEnv p = result.ProcessedEnvs[0];
            p.Keys.ShouldBe(["HELLO"]);
            p.EnvFilePath.ShouldBe(envFile);
            result.ChangedFilePaths.ShouldBe([envFile]);
        }
        finally
        {
            File.WriteAllText(envKeysFilePath, originalKeysFileContents);
        }
    }

    /// <remarks>Refer to "#run (finds .env file) and custom envKeysFilepath and privateKey already exists"</remarks>
    [Fact]
    public void TestRunFindsEnvFileAndCustomEnvKeysFilePathAndPrivateKeyAlreadyExists()
    {
        const string envKeysFilePath = "tests/monorepo/.env.keys";
        const string envFile = "tests/monorepo/apps/app1/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        EncryptResult result = new Encrypt(envs, null, null, envKeysFilePath).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        EncryptProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
    }

    /// <remarks>Refer to "#run (finds .env file only AND only the existing public key not the private key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileOnlyAndOnlyTheExistingPublicKeyNotThePrivateKey()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";
        string originalKeysFileContents = File.ReadAllText(envKeysFile);
        try
        {
            // Delete the .env.keys file so only the .env file is found
            File.Delete(envKeysFile);
            EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
            EncryptResult result = new Encrypt(envs, null, null, null).Run();

            result.ProcessedEnvs.ShouldHaveSingleItem();
            EncryptProcessedEnv p = result.ProcessedEnvs[0];
            p.Keys.ShouldBeEmpty();
            p.FilePath.ShouldBe(Path.GetFullPath(envFile));
            p.EnvFilePath.ShouldBe(envFile);
            result.UnchangedFilePaths.ShouldBe([envFile]);
        }
        finally
        {
            File.WriteAllText(envKeysFile, originalKeysFileContents);
        }
    }
}