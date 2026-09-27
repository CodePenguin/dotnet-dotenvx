using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

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
/// UPDATE 2026-09-24: `FILE_NOT_WRITABLE` translation was initially left unported here after an
/// earlier live test (icacls-denying WRITE on an EXISTING file) showed `dotenvx set` leaking the
/// raw EPERM message instead. That test was misleading, not evidence of unreachability - denying
/// write on an existing file also denies READ on Windows, so `setTransform`'s own read-phase catch
/// (`src/lib/transforms/set.js`) swallows the raw error before the later, correctly-translating
/// write call ever runs. A DIFFERENT live test (a brand-new file in a directory that denies file
/// CREATION specifically - icacls `/deny (WD,AD)`, or `chmod` removing write on the directory on
/// Linux - no read is ever attempted) confirmed the translation genuinely IS reachable and DOES
/// fire correctly on the real Node CLI. Ported here as `FileNotWritableException`, thrown from
/// `Fsx.WriteFileX`'s default implementation on `UnauthorizedAccessException` specifically (the
/// exception type confirmed live for this OS-level condition) - deliberately NOT
/// `IOException` (a locked-file sharing violation is a different condition upstream doesn't
/// translate either, and this port's own `TestSetOtherError`/`TestEncryptOtherError`/
/// `TestDecryptOtherError` tests already correctly lock in the raw-message behavior for that case).
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

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-27 bug. `DetectEncoding.cs` returns the
    /// literal string "utf16le" for a file starting with a UTF-16LE BOM (matching Node's own
    /// encoding name), but `ReadFileX`'s encoding switch only recognized "utf16", not "utf16le" -
    /// it fell through to `Encoding.GetEncoding("utf16le")`, which throws
    /// (`ArgumentException: 'utf16le' is not a supported encoding name`) since that literal
    /// string isn't a real .NET encoding name. Confirmed live: `dotenvx get` on a genuinely
    /// UTF-16LE-encoded .env file crashed with that exact message on every read.
    /// </remarks>
    [Fact]
    public void TestReadFileXUtf16LeEncoding()
    {
        File.WriteAllText("tests_fsx_somefile.txt", "hello", System.Text.Encoding.Unicode);

        string result = Fsx.ReadFileX("tests_fsx_somefile.txt", "utf16le");

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

    /// <remarks>
    /// Refer to "#writeFileXSync translates permission errors" / "#writeFileX translates permission
    /// errors". Uses a path that's actually an existing directory to portably trigger
    /// UnauthorizedAccessException on both Windows and Linux (CI) without OS-specific ACL/chmod
    /// setup - confirmed via a live probe that this reliably throws the same exception type as the
    /// real permission-denied scenario this translation targets.
    /// </remarks>
    [Fact]
    public void TestWriteFileXTranslatesPermissionErrors()
    {
        Directory.CreateDirectory("tests_fsx_a_directory");

        try
        {
            FileNotWritableException error = Should.Throw<FileNotWritableException>(() =>
                Fsx.WriteFileX("tests_fsx_a_directory", "hello"));

            error.MessageWithHelp.ShouldBe(
                "[FILE_NOT_WRITABLE] cannot write to file (tests_fsx_a_directory). fix: [https://github.com/dotenvx/dotenvx/issues/890]");
        }
        finally
        {
            Directory.Delete("tests_fsx_a_directory");
        }
    }
}
