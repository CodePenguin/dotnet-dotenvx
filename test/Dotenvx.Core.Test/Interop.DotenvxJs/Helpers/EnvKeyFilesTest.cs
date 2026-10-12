using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.EnvKeyFiles;
using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/readEnvKey.js, removeEnvKey.js and upsertEnvKey.js</remarks>
public class EnvKeyFilesTest : EnvironmentTestBase
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-envkey-" + Guid.NewGuid().ToString("N"));

    public EnvKeyFilesTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
    }

    protected override void TearDown()
    {
        base.TearDown();
        Environment.CurrentDirectory = Path.GetTempPath();
        Directory.Delete(_dir, true);
    }

    private string Keys => Path.Combine(_dir, ".env.keys");

    /// <remarks>Refer to "readEnvKey reads a key from an env file without decrypting"</remarks>
    [Fact]
    public void TestReadEnvKeyReadsAKeyFromAnEnvFileWithoutDecrypting()
    {
        string envFile = Path.Combine(_dir, ".env");
        File.WriteAllText(envFile, "DOTENV_PUBLIC_KEY=\"03abc\"\nHELLO=\"encrypted:abc123\"");

        ReadEnvKey("HELLO", envFile, true).ShouldBe("encrypted:abc123");
    }

    /// <remarks>Refer to "readEnvKey respects ignored missing key errors"</remarks>
    [Fact]
    public void TestReadEnvKeyRespectsIgnoredMissingKeyErrors()
    {
        string envFile = Path.Combine(_dir, ".env");
        File.WriteAllText(envFile, "HELLO=World");

        ReadEnvKey("MISSING", envFile, true, ["MISSING_KEY"]).ShouldBeNull();
        Should.Throw<MissingKeyException>(() => ReadEnvKey("MISSING", envFile, true)).Code.ShouldBe("MISSING_KEY");
    }

    /// <remarks>Refer to "#removeEnvKey removes key from existing .env.keys file"</remarks>
    [Fact]
    public void TestRemoveEnvKeyRemovesKeyFromExistingEnvKeysFile()
    {
        File.WriteAllText(Keys, "# comment\nDOTENV_PRIVATE_KEY=abc123\nOTHER=value\n");

        RemoveEnvKeyResult result = RemoveEnvKey("DOTENV_PRIVATE_KEY");

        result.Changed.ShouldBeTrue();
        result.Filepath.ShouldBe(".env.keys");
        File.ReadAllText(Keys).ShouldBe("# comment\nOTHER=value\n");
    }

    /// <remarks>Refer to "#removeEnvKey removes duplicate and export-prefixed entries"</remarks>
    [Fact]
    public void TestRemoveEnvKeyRemovesDuplicateAndExportPrefixedEntries()
    {
        File.WriteAllText(Keys, "DOTENV_PRIVATE_KEY=one\nOTHER=value\nexport DOTENV_PRIVATE_KEY=two\n");

        RemoveEnvKey("DOTENV_PRIVATE_KEY").Changed.ShouldBeTrue();

        File.ReadAllText(Keys).ShouldBe("OTHER=value\n");
    }

    /// <remarks>Refer to "#removeEnvKey preserves CRLF line endings"</remarks>
    [Fact]
    public void TestRemoveEnvKeyPreservesCrlfLineEndings()
    {
        File.WriteAllText(Keys, "DOTENV_PRIVATE_KEY=one\r\nOTHER=value\r\n");

        RemoveEnvKey("DOTENV_PRIVATE_KEY").Changed.ShouldBeTrue();

        File.ReadAllText(Keys).ShouldBe("OTHER=value\r\n");
    }

    /// <remarks>Refer to "#removeEnvKey does not rewrite file when key is already absent"</remarks>
    [Fact]
    public void TestRemoveEnvKeyDoesNotRewriteFileWhenKeyIsAlreadyAbsent()
    {
        File.WriteAllText(Keys, "OTHER=value\n");
        DateTime before = File.GetLastWriteTimeUtc(Keys);
        Thread.Sleep(30);

        RemoveEnvKey("DOTENV_PRIVATE_KEY").Changed.ShouldBeFalse();

        File.ReadAllText(Keys).ShouldBe("OTHER=value\n");
        File.GetLastWriteTimeUtc(Keys).ShouldBe(before, "does not rewrite unchanged file");
    }

    /// <remarks>Refer to "#removeEnvKey leaves an empty .env.keys file unchanged"</remarks>
    [Fact]
    public void TestRemoveEnvKeyLeavesAnEmptyEnvKeysFileUnchanged()
    {
        File.WriteAllText(Keys, "");

        RemoveEnvKey("DOTENV_PRIVATE_KEY").Changed.ShouldBeFalse();

        File.ReadAllText(Keys).ShouldBe("");
    }

    /// <remarks>Refer to "#removeEnvKey deletes file when removing the last key"</remarks>
    [Fact]
    public void TestRemoveEnvKeyDeletesFileWhenRemovingTheLastKey()
    {
        File.WriteAllText(Keys, "DOTENV_PRIVATE_KEY=abc123\n");

        RemoveEnvKey("DOTENV_PRIVATE_KEY").Changed.ShouldBeTrue();

        File.Exists(Keys).ShouldBeFalse();
    }

    /// <remarks>Refer to "#removeEnvKey deletes file when only comments remain after removing last key"</remarks>
    [Fact]
    public void TestRemoveEnvKeyDeletesFileWhenOnlyCommentsRemainAfterRemovingLastKey()
    {
        File.WriteAllText(Keys, "# comment\nDOTENV_PRIVATE_KEY=abc123\n");

        RemoveEnvKey("DOTENV_PRIVATE_KEY").Changed.ShouldBeTrue();

        File.Exists(Keys).ShouldBeFalse();
    }

    /// <remarks>Refer to "#removeEnvKey returns unchanged when .env.keys is missing"</remarks>
    [Fact]
    public void TestRemoveEnvKeyReturnsUnchangedWhenEnvKeysIsMissing()
    {
        RemoveEnvKey("DOTENV_PRIVATE_KEY").ShouldBe(new RemoveEnvKeyResult(false, "DOTENV_PRIVATE_KEY", ".env.keys"));
    }

    /// <remarks>Refer to "#upsertEnvKey creates .env.keys when missing"</remarks>
    [Fact]
    public void TestUpsertEnvKeyCreatesEnvKeysWhenMissing()
    {
        UpsertEnvKeyResult result = UpsertEnvKey("DOTENV_PRIVATE_KEY", "abc123");

        result.Changed.ShouldBeTrue();
        result.Created.ShouldBeTrue();
        result.Filepath.ShouldBe(".env.keys");
        File.ReadAllText(Keys).ShouldBe("DOTENV_PRIVATE_KEY=abc123\n");
    }

    /// <remarks>Refer to "#upsertEnvKey appends key when missing from existing file"</remarks>
    [Fact]
    public void TestUpsertEnvKeyAppendsKeyWhenMissingFromExistingFile()
    {
        File.WriteAllText(Keys, "# comments stay\nEXISTING=1\n");

        UpsertEnvKeyResult result = UpsertEnvKey("DOTENV_PRIVATE_KEY_PRODUCTION", "prod123");

        result.Changed.ShouldBeTrue();
        result.Created.ShouldBeFalse();
        File.ReadAllText(Keys).ShouldBe("# comments stay\nEXISTING=1\nDOTENV_PRIVATE_KEY_PRODUCTION=prod123\n");
    }

    /// <remarks>Refer to "#upsertEnvKey does not rewrite file when value is already current"</remarks>
    [Fact]
    public void TestUpsertEnvKeyDoesNotRewriteFileWhenValueIsAlreadyCurrent()
    {
        File.WriteAllText(Keys, "DOTENV_PRIVATE_KEY=samevalue\n");
        DateTime before = File.GetLastWriteTimeUtc(Keys);
        Thread.Sleep(30);

        UpsertEnvKeyResult result = UpsertEnvKey("DOTENV_PRIVATE_KEY", "samevalue");

        result.Changed.ShouldBeFalse();
        result.Created.ShouldBeFalse();
        File.ReadAllText(Keys).ShouldBe("DOTENV_PRIVATE_KEY=samevalue\n");
        File.GetLastWriteTimeUtc(Keys).ShouldBe(before, "does not rewrite unchanged file");
    }

    /// <remarks>Refer to "#upsertEnvKey replaces existing values, removes duplicates, and avoids quotes"</remarks>
    [Fact]
    public void TestUpsertEnvKeyReplacesExistingValuesRemovesDuplicatesAndAvoidsQuotes()
    {
        File.WriteAllText(Keys,
            string.Join("\n", "# .env", "DOTENV_PRIVATE_KEY=\"old-quoted\"", "OTHER=value", "export DOTENV_PRIVATE_KEY=old-unquoted", ""));

        UpsertEnvKey("DOTENV_PRIVATE_KEY", "newvalue");

        File.ReadAllText(Keys).ShouldBe("# .env\nDOTENV_PRIVATE_KEY=newvalue\nOTHER=value\n");
    }

    /// <remarks>Refer to "#upsertEnvKey preserves CRLF line endings"</remarks>
    [Fact]
    public void TestUpsertEnvKeyPreservesCrlfLineEndings()
    {
        File.WriteAllText(Keys, "OTHER=value\r\n");

        UpsertEnvKey("DOTENV_PRIVATE_KEY", "abc123");

        File.ReadAllText(Keys).ShouldBe("OTHER=value\r\nDOTENV_PRIVATE_KEY=abc123\r\n");
    }
}
