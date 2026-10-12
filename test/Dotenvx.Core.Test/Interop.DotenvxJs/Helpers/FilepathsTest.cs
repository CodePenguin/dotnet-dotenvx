using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/conventions/filepaths.test.js</remarks>
public class FilepathsTest
{
    /// <remarks>Refer to "filepaths defaults to .env"</remarks>
    [Fact]
    public void TestDefaultsToEnv()
    {
        Filepaths().ShouldBe([".env"]);
    }

    /// <remarks>Refer to "filepaths wraps a string"</remarks>
    [Fact]
    public void TestWrapsAString()
    {
        Filepaths(".env.production").ShouldBe([".env.production"]);
    }

    /// <remarks>Refer to "filepaths returns arrays as-is"</remarks>
    [Fact]
    public void TestReturnsArraysAsIs()
    {
        string[] paths = [".env", ".env.production"];

        Filepaths(paths).ShouldBeSameAs(paths);
    }
}
