using System.Text;

using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/detectEncoding.test.js</remarks>
public class DetectEncodingTest : EnvironmentTestBase
{
    /// <remarks>Refer to "#detectEncoding utf8"</remarks>
    [Fact]
    public void TestDetectEncodingUtf8()
    {
        // Mock ReadFileSync to return UTF-8 bytes without BOM
        Fsx.ReadFileSync = path => Encoding.UTF8.GetBytes("BASIC=basic\n");

        string encoding = DetectEncoding("tests/.env");

        encoding.ShouldBe("utf8");
    }

    /// <remarks>Refer to "#detectEncoding utf16le"</remarks>
    [Fact]
    public void TestDetectEncodingUtf16Le()
    {
        // Mock ReadFileSync to return UTF-16LE BOM + content
        byte[] utf16LeBom = [0xFF, 0xFE];
        byte[] content = Encoding.Unicode.GetBytes("BASIC=basic\n");
        byte[] buffer = utf16LeBom.Concat(content).ToArray();

        Fsx.ReadFileSync = path => buffer;

        string encoding = DetectEncoding("tests/.env.utf16le");

        encoding.ShouldBe("utf16le");
    }

    /// <remarks>Refer to "#detectEncoding fallback utf8 (latin1)"</remarks>
    [Fact]
    public void TestDetectEncodingFallbackUtf8Latin1()
    {
        // Mock ReadFileSync to return bytes without BOM (simulating latin1 or other)
        Fsx.ReadFileSync = path => new byte[] { 0x42, 0x41, 0x53, 0x49, 0x43 }; // "BASIC" in ASCII

        string encoding = DetectEncoding("tests/.env.latin1");

        encoding.ShouldBe("utf8");
    }
}