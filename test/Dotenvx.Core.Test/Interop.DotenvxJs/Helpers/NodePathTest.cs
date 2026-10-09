using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Not from a JS test: path.resolve and path.basename semantics checked against Node (see docs/porting-traps.md #88).</remarks>
public class NodePathTest
{
    [Theory]
    [InlineData(".env/")]
    [InlineData(".env//")]
    [InlineData("./.env/")]
    public void ResolveDropsTrailingSeparators(string path)
    {
        NodePath.Resolve(path).ShouldBe(Path.GetFullPath(".env"));
    }

    [Fact]
    public void ResolveKeepsARoot()
    {
        string root = Path.GetPathRoot(Path.GetFullPath("."))!;

        NodePath.Resolve(root).ShouldBe(root);
    }

    [Theory]
    [InlineData(".env", ".env")]
    [InlineData(".env/", ".env")]
    [InlineData("sub/.env", ".env")]
    [InlineData("sub/.env//", ".env")]
    [InlineData("sub/", "sub")]
    [InlineData("", "")]
    [InlineData("/", "")]
    public void BasenameIgnoresTrailingSeparators(string path, string expected)
    {
        NodePath.Basename(path).ShouldBe(expected);
    }
}
