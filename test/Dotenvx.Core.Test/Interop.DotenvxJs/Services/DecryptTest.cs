using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/decrypt.test.js</remarks>
public class DecryptTest : EnvironmentTestBase
{
    // the real transform reads keys from `fk` (default `.env.keys` in the working directory), not from the env file directory.
    // `fk` is a list at this pin: decrypt.js passes `options.envKeysFile` straight through, which `collectEnvKeys` fills with
    // one entry per repeated -fk.
    private static string[] KeysFor(string envFile)
    {
        return [Path.Combine(Path.GetDirectoryName(envFile)!, ".env.keys").Replace((char)92, (char)47)];
    }

    public DecryptTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "#run (no arguments)"</remarks>
    [Fact]
    public void TestRunNoArguments()
    {
        DecryptResult result = new Decrypt([], null, null, null).Run();

        MissingEnvFileException exampleError = new(".env");

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

        MissingEnvFileException exampleError = new(".env");

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
        DecryptResult result = new Decrypt(envs, null, null, KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        // Parse the envSrc
        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("encrypted");
    }

    /// <remarks>
    /// Refer to "#run (finds .env file with multiline value)". Live-verified against the real Node CLI:
    /// `decrypt` upserts every parsed value, so the double-quoted backslash-n escapes in ALOHA come back
    /// as real newlines (HELLO is already multiline and unchanged).
    /// </remarks>
    [Fact]
    public void TestRunFindsEnvFileWithMultilineValue()
    {
        const string envFile = "tests/monorepo/apps/multiline/.env";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, null, KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["ALOHA"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
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
            ALOHA="-----BEGIN RSA PRIVATE KEY-----
            ABCD
            EFGH
            IJKL
            -----END RSA PRIVATE KEY-----"

            """);
    }

    /// <remarks>Refer to "#run (finds .env file with specified key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, ["HELLO2"], null, KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
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
        DecryptResult result = new Decrypt(envs, ["HELLO2"], null, KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO2"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
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
        DecryptResult result = new Decrypt(envs, ["HE*"], null, KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO2", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
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
        DecryptResult result = new Decrypt(envs, null, ["HELLO2"], KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
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
        DecryptResult result = new Decrypt(envs, null, ["HELLO2"], KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO", "HELLO3"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldBe("one");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldBe("three");
    }

    /// <remarks>
    /// Refer to "#decrypt - partially decrypts when another encrypted value is bad" - from
    /// dotenvx/tests/e2e/decrypt.test.js, not this file's usual lib/services/decrypt.test.js (that
    /// upstream file has no equivalent scenario). Verified live against the real Node CLI, 2026-09-22:
    /// found and fixed two real bugs while porting this - Decrypt.cs's per-key loop had no try/catch,
    /// so one malformed value aborted decryption of every other (successfully-decryptable) key in the
    /// same file, and DecryptAction.cs's `else if (Changed)` made Error/Changed mutually exclusive,
    /// silently dropping the partially-decrypted output whenever any key failed. Also: the specific
    /// underlying failure reason (malformed data, wrong key, missing key) is NOT surfaced here - it's
    /// unconditionally the generic "could not decrypt {key}", confirmed by calling
    /// @dotenvx/primitives' parsearrays() (used only by the `decrypt` command) directly with a
    /// missing key, a wrong-but-validly-shaped key, and malformed ciphertext - all three produced the
    /// exact same generic message. This is different from run/get/set, which use primitives' singular
    /// parse() and DO surface the specific WRONG_PRIVATE_KEY/MISSING_PRIVATE_KEY cause (see
    /// RunActionTest.cs's TestRunWrongPrivateKey/TestRunMissingPrivateKey).
    /// </remarks>
    [Fact]
    public void TestRunPartiallyDecryptsWhenAnotherEncryptedValueIsBad()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env.keys", ".env.keys");
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env");
        File.AppendAllText(".env", "FAKE=\"encrypted:fake123345343434\"\n");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        DecryptResult result = new Decrypt(envs, null, null, null).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.Changed.ShouldBeTrue();
        p.Error.ShouldNotBeNull();
        p.Error.ShouldBeOfType<DecryptionFailedException>();
        ((DotenvxException)p.Error).MessageWithHelp.ShouldBe(
            "[DECRYPTION_FAILED] could not decrypt FAKE. fix: [https://github.com/dotenvx/dotenvx/issues/757]");

        result.ChangedFilePaths.ShouldBe([".env"]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        p.EnvSrc.ShouldContain("HELLO=\"encrypted\"");
        p.EnvSrc.ShouldContain("FAKE=\"encrypted:fake123345343434\"");
    }

    /// <remarks>Refer to "#run (finds .env file with excluded key glob string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithExcludedKeyGlobString()
    {
        const string envFile = "tests/monorepo/apps/multiple/.env.production";
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];
        DecryptResult result = new Decrypt(envs, null, ["HE*"], KeysFor(envFile)).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        ProcessedDecrypt p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([envFile]);

        Dictionary<string, object> parsed = ScanToDictionary(p.EnvSrc);
        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY_PRODUCTION", "HELLO", "HELLO2", "HELLO3"]);
        (parsed["DOTENV_PUBLIC_KEY_PRODUCTION"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO2"] as string).ShouldStartWith("encrypted:");
        (parsed["HELLO3"] as string).ShouldStartWith("encrypted:");
    }
}
