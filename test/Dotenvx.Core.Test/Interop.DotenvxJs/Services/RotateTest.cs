using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/rotate.test.js</remarks>
public class RotateTest : EnvironmentTestBase
{
    public RotateTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "#run (no arguments)"</remarks>
    [Fact]
    public void TestRunNoArguments()
    {
        RotateResult result = new Rotate([]).Run();

        MissingEnvFileException exampleError = new(".env");

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([".env"]);
    }

    /// <remarks>Refer to "#run (no env file)"</remarks>
    [Fact]
    public void TestRunNoEnvFile()
    {
        RotateResult result = new Rotate([]).Run();

        MissingEnvFileException exampleError = new(".env");

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([".env"]);
    }

    /// <remarks>
    /// Not from a JS test - regression test for a real 2026-09-26 bug found while auditing
    /// Rotate.cs against the historical services/rotate.js: a missing .env.keys (with .env
    /// present) was reported as "[MISSING_ENV_FILE] missing file (.env)" - wrong file, and a
    /// confusing message since .env genuinely exists. The historical source distinguishes the two
    /// by comparing the ENOENT error's own path against the expected .env.keys path; this port
    /// checks existence explicitly instead (see Rotate.cs's own comment) and now throws the real
    /// upstream MISSING_ENV_KEYS_FILE code (errors.js's missingEnvKeysFile(), which still exists
    /// at this pin even though rotate.js itself doesn't).
    /// </remarks>
    [Fact]
    public void TestRunMissingEnvKeysFileReportsTheRightFile()
    {
        File.WriteAllText(".env", "DOTENV_PUBLIC_KEY=\"deadbeef\"\nHELLO=\"encrypted:abc\"\n");

        RotateResult result = new Rotate([]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        DotenvxException error = p.Error.ShouldBeOfType<MissingEnvKeysFileException>();
        error.Code.ShouldBe("MISSING_ENV_KEYS_FILE");
        // The historical source reports the bare, non-resolved join result (".env.keys" for a
        // top-level .env); this port's row.FilePath is deliberately always absolute (needed for
        // Fsx.ReadFileX elsewhere), so the .env.keys path derived from it is absolute too - a
        // minor cosmetic difference from a feature with no live upstream CLI to diff against.
        error.MessageWithHelp.ShouldEndWith(
            $"{Path.DirectorySeparatorChar}.env.keys). fix: [https://github.com/dotenvx/dotenvx/issues/775]");
        error.MessageWithHelp.ShouldStartWith("[MISSING_ENV_KEYS_FILE] missing file (");
    }

    /// <remarks>Refer to "#run (no arguments and some other error)"</remarks>
    [Fact]
    public void TestRunNoArgumentsAndSomeOtherError()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => throw new Exception("Mock Error");
        Fsx.ReadFileSync = _ => []; // Default to utf8 for DetectEncoding

        RotateResult result = new Rotate([]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Error.ShouldBeOfType<Exception>();
        p.Error.Message.ShouldStartWith("Mock Error");
        p.EnvFilePath.ShouldBe(".env");
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (finds .env file)"</remarks>
    [Fact]
    public void TestRunFindsEnvFile()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file with specified glob string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedGlobString()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, ["H*"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>
    /// Not from a JS test - regression test for a real 2026-09-26 bug found while auditing
    /// Rotate.cs against the historical services/rotate.js: `--exclude-key` given WITHOUT
    /// `--key` silently skipped re-encrypting EVERY key, not just the excluded one - confirmed
    /// live (rotate --exclude-key FOO changed DOTENV_PUBLIC_KEY but left HELLO, which was never
    /// excluded, encrypted under the OLD key). Root cause: Picomatch(null, someNonEmptyExcludeList)
    /// doesn't hit its own "match everything" special case (that only fires when the ignore list
    /// is ALSO empty), so the missing `key.Length > 0` guard (present in the historical source's
    /// `if (this.keys.length > 0 &amp;&amp; !this.include(key))`) let every key fail the include
    /// check. This test builds its own two-key encrypted fixture (the existing
    /// tests/monorepo/apps/encrypted/.env fixture only has one key, so it can't distinguish
    /// "nothing rotated" from "only the excluded key wasn't rotated").
    /// </remarks>
    [Fact]
    public void TestRunExcludingKeyWithoutSpecifyingKeyStillRotatesOtherKeys()
    {
        const string publicKey = "03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba";
        const string privateKey = "ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1";
        string encryptedHello = EncryptValue("World", publicKey);
        string encryptedFoo = EncryptValue("bar", publicKey);

        File.WriteAllText(".env",
            $"DOTENV_PUBLIC_KEY=\"{publicKey}\"\nHELLO=\"{encryptedHello}\"\nFOO=\"{encryptedFoo}\"\n");
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY=\"{privateKey}\"\n");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        RotateResult result = new Rotate(envs, null, ["FOO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);
        (parsed["HELLO"] as string).ShouldNotBe(encryptedHello);
        (parsed["FOO"] as string).ShouldBe(encryptedFoo);
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key as string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key globbed)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKeyGlobbed()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, ["HE*"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file) and custom envKeysFilepath"</remarks>
    [Fact]
    public void TestRunFindsEnvFileAndCustomEnvKeysFilepath()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, null, envKeysFile).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }
}