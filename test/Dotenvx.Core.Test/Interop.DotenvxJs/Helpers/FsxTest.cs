using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/fsx.js. Upstream's JS test (fsx.test.js) mocks
/// `fs`/`fs.promises` directly and asserts the wrapper functions call through with the right
/// encoding/args, and separately verifies `writeFileXSync`/`writeFileX` translate EACCES/EPERM
/// into a friendly `FILE_NOT_WRITABLE` error. This port's `Fsx` statics ARE the file I/O (no
/// separate translation layer to verify in isolation via mocking - see the delegate-swap-statics
/// convention in CLAUDE.md), so these tests exercise the real default implementations against
/// real temp files instead of mocking `System.IO` calls.
///
/// The `FILE_NOT_WRITABLE` translation is intentionally NOT ported here - confirmed via live
/// testing against the real Node CLI (2026-09-22, using icacls to deny write access to a file,
/// matching a genuine EPERM on Windows) that `dotenvx set` does NOT surface the translated
/// message even though `fsx.writeFileX`'s try/catch should apply on that exact code path
/// (`src/cli/actions/set.js` calls `fsx.writeFileX` directly) - it leaks the raw
/// `EPERM: operation not permitted, open '...'` message instead, matching what this port already
/// does (see TestSetOtherError/TestEncryptOtherError/TestDecryptOtherError in the corresponding
/// *ActionTest.cs files). Whether this is a latent upstream bug, a Windows-specific quirk, or
/// something about this pin's specific EPERM shape not matching what fsx.js's catch expects,
/// wasn't fully run to ground - flagged as a "needs a decision" item in .claude/plan.md rather
/// than guessing at a fix that would newly diverge from confirmed-live-correct current behavior.
/// </remarks>
public class FsxTest : EnvironmentTestBase
{
    protected override void TearDown()
    {
        base.TearDown();
        File.Delete("tests_fsx_somefile.txt");
    }

    /// <remarks>Refer to "#readFileX (default utf8)"</remarks>
    [Fact]
    public void TestReadFileXDefaultUtf8()
    {
        File.WriteAllText("tests_fsx_somefile.txt", "hello");

        string result = Fsx.ReadFileX("tests_fsx_somefile.txt");

        result.ShouldBe("hello");
    }

    /// <remarks>Refer to "#readFileX (explicit encoding options)"</remarks>
    [Fact]
    public void TestReadFileXExplicitEncoding()
    {
        File.WriteAllText("tests_fsx_somefile.txt", "hello", System.Text.Encoding.ASCII);

        string result = Fsx.ReadFileX("tests_fsx_somefile.txt", "ascii");

        result.ShouldBe("hello");
    }

    /// <remarks>Refer to "#writeFileXSync"</remarks>
    [Fact]
    public void TestWriteFileXSync()
    {
        // This port's equivalent of writeFileXSync is Fsx.WriteFileSync (there is no separate
        // sync/async split here - both read as synchronous C# calls; WriteFileX matches
        // fsx.writeFileX's utf8-no-BOM behavior specifically, see TestWriteFileX below).
        Fsx.WriteFileSync("tests_fsx_somefile.txt", "hello");

        File.ReadAllText("tests_fsx_somefile.txt").ShouldBe("hello");
    }

    /// <remarks>Refer to "#writeFileX"</remarks>
    [Fact]
    public void TestWriteFileX()
    {
        Fsx.WriteFileX("tests_fsx_somefile.txt", "hello");

        byte[] bytes = File.ReadAllBytes("tests_fsx_somefile.txt");
        bytes.ShouldNotBeEmpty();
        // No UTF-8 BOM (0xEF 0xBB 0xBF) - matches Node's fs.promises.writeFile, which never emits one.
        bytes[0].ShouldNotBe((byte)0xEF);
        File.ReadAllText("tests_fsx_somefile.txt").ShouldBe("hello");
    }

    /// <remarks>Refer to "#exists"</remarks>
    [Fact]
    public void TestExists()
    {
        File.WriteAllText("tests_fsx_somefile.txt", "hello");

        Fsx.ExistsSync("tests_fsx_somefile.txt").ShouldBeTrue();
        Fsx.ExistsSync("tests_fsx_missing.txt").ShouldBeFalse();
    }
}
