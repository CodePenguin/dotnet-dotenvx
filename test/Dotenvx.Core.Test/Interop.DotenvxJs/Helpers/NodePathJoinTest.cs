using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Not from a JS test - expected values confirmed live against Node's `path.join` (win32) on 2026-10-02.
/// </remarks>
public class NodePathJoinTest
{
    private static string Sep(string s) => s.Replace('/', Path.DirectorySeparatorChar);

    [Theory]
    [InlineData("a", "../b", "b")]
    [InlineData("", "x", "x")]
    [InlineData("", "", ".")]
    [InlineData(".", "x", "x")]
    [InlineData("./config", ".env", "config/.env")]
    [InlineData("a", "/foo", "a/foo")]
    [InlineData("a", "", "a")]
    public void JoinsLikeNodePathJoin(string directory, string file, string expected)
    {
        NodePathJoin(directory, file).ShouldBe(Sep(expected));
    }

    /// <remarks>Not from a JS test - `~/x` must stay under the home dir (Path.Combine would discard it).</remarks>
    [Fact]
    public void TildePathStaysUnderHome()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        EnvItem[] envs = BuildEnvs(new ConfigOptions { Path = ["~/foo/.env", "~"] });

        envs[0].Value.ShouldBe(NodePathJoin(home, "foo/.env"));
        envs[1].Value.ShouldBe(NodePathJoin(home, ""));
    }
}
