using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/tests/lib/transforms/del.test.js. Behaviors below were verified directly
/// against the real dotenvx v2.24.1 Node CLI (byte-for-byte diffed output) since the underlying
/// @dotenvx/primitives `remove` implementation isn't vendored source in this submodule.
/// </remarks>
public class DelTest : EnvironmentTestBase
{
    public DelTest()
    {
        ClearEnvironment();
        Fsx.ReadFileSync = _ => []; // DetectEncoding defaults to utf8 for an empty buffer
    }

    /// <remarks>Refer to "removes a simple middle key, leaving no blank line"</remarks>
    [Fact]
    public void TestRunRemovesSimpleMiddleKey()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=World\nFOO=bar\nBAZ=qux\n";

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = ".env" }];
        DelResult result = new Del("FOO", envs).Run();

        result.ProcessedEnvs.Length.ShouldBe(1);
        ProcessedDel row = result.ProcessedEnvs[0];
        row.Key.ShouldBe("FOO");
        row.Changed.ShouldBeTrue();
        row.EnvSrc.ShouldBe("HELLO=World\nBAZ=qux\n");
        result.ChangedFilePaths.ShouldBe([".env"]);
    }

    /// <remarks>Refer to "removes the first key without leaving a leading blank line"</remarks>
    [Fact]
    public void TestRunRemovesFirstKey()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=World\nFOO=bar\n";

        DelResult result = new Del("HELLO", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("FOO=bar\n");
    }

    /// <remarks>Refer to "removes the last key, preserving no trailing blank line"</remarks>
    [Fact]
    public void TestRunRemovesLastKey()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=World\nFOO=bar\n";

        DelResult result = new Del("FOO", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("HELLO=World\n");
    }

    /// <remarks>Refer to "removes the only key, leaving an empty file"</remarks>
    [Fact]
    public void TestRunRemovesOnlyKey()
    {
        Fsx.ReadFileX = (_, _) => "ONLY=value\n";

        DelResult result = new Del("ONLY", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("");
    }

    /// <remarks>Refer to "removes the last key when the file has no trailing newline"</remarks>
    [Fact]
    public void TestRunRemovesLastKeyWithNoTrailingNewline()
    {
        Fsx.ReadFileX = (_, _) => "A=1\nB=2";

        DelResult result = new Del("B", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("A=1");
    }

    /// <remarks>Refer to "removes an exported key"</remarks>
    [Fact]
    public void TestRunRemovesExportedKey()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=World\nexport BAZ=qux\nAFTER=x\n";

        DelResult result = new Del("BAZ", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("HELLO=World\nAFTER=x\n");
    }

    /// <remarks>Refer to "removes a key with a trailing inline comment"</remarks>
    [Fact]
    public void TestRunRemovesKeyWithTrailingComment()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=World\nFOO=bar # comment\nAFTER=x\n";

        DelResult result = new Del("FOO", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("HELLO=World\nAFTER=x\n");
    }

    /// <remarks>Refer to "removes a multiline double-quoted key"</remarks>
    [Fact]
    public void TestRunRemovesMultilineKey()
    {
        Fsx.ReadFileX = (_, _) => "HELLO=\"World\"\nMULTI=\"line1\nline2\"\nAFTER=x\n";

        DelResult result = new Del("MULTI", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        result.ProcessedEnvs[0].EnvSrc.ShouldBe("HELLO=\"World\"\nAFTER=x\n");
    }

    /// <remarks>Refer to "leaves the file unchanged when the key is not found"</remarks>
    [Fact]
    public void TestRunKeyNotFound()
    {
        Fsx.ReadFileX = (_, _) => "A=1\n";

        DelResult result = new Del("NOTFOUND", [new() { Type = EnvType.EnvFile, Value = ".env" }]).Run();

        ProcessedDel row = result.ProcessedEnvs[0];
        row.Changed.ShouldBeFalse();
        row.EnvSrc.ShouldBe("A=1\n");
        result.UnchangedFilePaths.ShouldBe([".env"]);
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "errors when the env file is missing"</remarks>
    [Fact]
    public void TestRunMissingEnvFile()
    {
        DelResult result = new Del("FOO", [new() { Type = EnvType.EnvFile, Value = "nonexistent.env" }]).Run();

        ProcessedDel row = result.ProcessedEnvs[0];
        row.Error.ShouldBeOfType<MissingEnvFileException>();
        ((MissingEnvFileException)row.Error!).Code.ShouldBe("MISSING_ENV_FILE");
        row.Changed.ShouldBeFalse();
    }

    /// <remarks>Refer to "defaults to .env when no env file is specified"</remarks>
    [Fact]
    public void TestRunDefaultsToDotEnv()
    {
        Fsx.ReadFileX = (_, _) => "FOO=bar\n";

        DelResult result = new Del("FOO", []).Run();

        result.ProcessedEnvs[0].EnvFilePath.ShouldBe(".env");
        result.ProcessedEnvs[0].Changed.ShouldBeTrue();
    }
}
