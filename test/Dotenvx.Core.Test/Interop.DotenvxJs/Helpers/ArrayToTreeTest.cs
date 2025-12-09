using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/arrayToTree.test.js</remarks>
public class ArrayToTreeTest
{
    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRun()
    {
        string[] arr =
        [
            ".env",
            "sub1/.env",
            "sub1/sub2/.env",
            "sub1/sub2/sub3/.env"
        ];

        Dictionary<string, object> tree = ArrayToTree(arr);

        tree.ShouldContainKey(".env");
        ((Dictionary<string, object>)tree[".env"]).ShouldBeEmpty();

        tree.ShouldContainKey("sub1");
        Dictionary<string, object> sub1 = (Dictionary<string, object>)tree["sub1"];
        sub1.ShouldContainKey(".env");
        ((Dictionary<string, object>)sub1[".env"]).ShouldBeEmpty();

        sub1.ShouldContainKey("sub2");
        Dictionary<string, object> sub2 = (Dictionary<string, object>)sub1["sub2"];
        sub2.ShouldContainKey(".env");
        ((Dictionary<string, object>)sub2[".env"]).ShouldBeEmpty();

        sub2.ShouldContainKey("sub3");
        Dictionary<string, object> sub3 = (Dictionary<string, object>)sub2["sub3"];
        sub3.ShouldContainKey(".env");
        ((Dictionary<string, object>)sub3[".env"]).ShouldBeEmpty();
    }
}