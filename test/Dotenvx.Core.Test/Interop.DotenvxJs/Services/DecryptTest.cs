using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/decrypt.test.js</remarks>
public class DecryptTest : EnvironmentTestBase
{
    public DecryptTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "#run (no arguments)"</remarks>
    [Fact]
    public void TestRunNoArguments()
    {
        DecryptResult result = new Decrypt([], null, null, null).Run();

        MissingEnvFileException exampleError = new(".env", Path.GetFullPath(".env"));

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no env file)"</remarks>
    [Fact]
    public void TestRunNoEnvFile()
    {
        DecryptResult result = new Decrypt([], null, null, null).Run();

        MissingEnvFileException exampleError = new(".env", Path.GetFullPath(".env"));

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (no arguments and some other error)"</remarks>
    [Fact]
    public void TestRunNoArgumentsAndSomeOtherError()
    {
        // Simulate an error within the Decrypt.Run
        using FileStream fs = new(".env", FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(""u8);

        DecryptResult result = new Decrypt([], null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Error.ShouldBeOfType<IOException>();
        p.Error.Message.ShouldStartWith("The process cannot access the file");
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (finds .env file)"</remarks>
    [Fact]
    public void TestRunFindsEnvFile()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        // Parse the envSrc
        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("encrypted");
    }

    /// <remarks>Refer to "#run (finds .env file with multiline value)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithMultilineValue()
    {
        const string envFile = "tests/monorepo/apps/multiline/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([envFile]);

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["HELLO", "ALOHA"]);
        (parsed["HELLO"] as string).ShouldBe(
            """
            -----BEGIN RSA PRIVATE KEY-----
            ABCD
            EFGH
            IJKL
            -----END RSA PRIVATE KEY-----
            """);

        p.EnvSrc.ShouldBe(
            """
            HELLO="-----BEGIN RSA PRIVATE KEY-----
            ABCD
            EFGH
            IJKL
            -----END RSA PRIVATE KEY-----"
            ALOHA="-----BEGIN RSA PRIVATE KEY-----\nABCD\nEFGH\nIJKL\n-----END RSA PRIVATE KEY-----"

            """);
    }

    /// <remarks>Refer to "#run (finds .env file with specified key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, ["HELLO2"], null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldBe("two");
        (parsed["HELLO3"] as string).ShouldStartWith("encrypted:");
    }

    /// <remarks>Refer to "#run (finds .env file with specified key as string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, ["HELLO2"], null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldBe("two");
        (parsed["HELLO3"] as string).ShouldStartWith("encrypted:");
    }

    /// <remarks>Refer to "#run (finds .env file with specified key as glob pattern)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKeyAsGlobPattern()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, ["HE*"], null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO2", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldBe("two");
        (parsed["HELLO3"] as string).ShouldBe("three");
    }

    /// <remarks>Refer to "#run (finds .env file with excluded key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithExcludedKey()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, ["HELLO2"], null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldBe("three");
    }

    /// <remarks>Refer to "#run (finds .env file with excluded key as string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithExcludedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, ["HELLO2"], null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldBe("three");
    }

    /// <remarks>Refer to "#run (finds .env file with excluded key glob string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithExcludedKeyGlobString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, ["HE*"], null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([envFile]);

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldStartWith("encrypted:");
    }
}