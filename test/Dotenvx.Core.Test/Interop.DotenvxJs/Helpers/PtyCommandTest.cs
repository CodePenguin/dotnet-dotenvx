using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/ptyCommand.js</remarks>
public class PtyCommandTest : EnvironmentTestBase
{
    public PtyCommandTest()
    {
        PtyCommand.Which = _ => Path.Combine(Path.GetTempPath(), "bin", "script");
    }

    /// <remarks>Refer to "ptyCommand wraps a command for macOS script without shell quoting"</remarks>
    [Fact]
    public void TestPtyCommandWrapsACommandForMacosScriptWithoutShellQuoting()
    {
        string[]? result = PtyCommand.Wrap(["/path/with spaces/claude", "it's", "--flag"], "darwin");

        Path.GetFileName(result![0]).ShouldBe("script");
        result[1..].ShouldBe(["-q", "/dev/null", "/path/with spaces/claude", "it's", "--flag"]);
    }

    /// <remarks>Refer to "ptyCommand safely quotes a command for util-linux script"</remarks>
    [Fact]
    public void TestPtyCommandSafelyQuotesACommandForUtilLinuxScript()
    {
        string[]? result = PtyCommand.Wrap(["/usr/bin/claude", "hello world", "it's"], "linux");

        Path.GetFileName(result![0]).ShouldBe("script");
        result[1..].ShouldBe(["-qefc", "'/usr/bin/claude' 'hello world' 'it'\"'\"'s'", "/dev/null"]);
    }

    /// <remarks>Refer to "ptyCommand is unavailable on unsupported platforms"</remarks>
    [Fact]
    public void TestPtyCommandIsUnavailableOnUnsupportedPlatforms()
    {
        PtyCommand.Wrap(["claude"], "win32").ShouldBeNull();
    }

    /// <remarks>Not from a JS test - `which.sync('script')` failing means "no pty" (null), not an error.</remarks>
    [Fact]
    public void TestPtyCommandIsUnavailableWithoutScript()
    {
        PtyCommand.Which = _ => null;

        PtyCommand.Wrap(["claude"], "linux").ShouldBeNull();
    }
}
