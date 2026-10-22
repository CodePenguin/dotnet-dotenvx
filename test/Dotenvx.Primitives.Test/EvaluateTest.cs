using System.Runtime.InteropServices;


using Shouldly;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Primitives.Test;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/evalKeyValue.js. No longer present at the currently
/// pinned commit - moved into the external, unvendored @dotenvx/primitives package - so this points
/// at its last readable version (pre-primitives-extraction, commit 882ea84c) for behavior parity.
/// </remarks>
public class EvaluateTest
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <remarks>Refer to "#evalKeyValue with a simple command"</remarks>
    [Fact]
    public void TestEvalKeyValueExecutesSimpleCommand()
    {
        string result = Evaluate("EVAL", "$(echo world)", new Dictionary<string, string>(), []);

        result.ShouldBe("world");
    }

    /// <remarks>Refer to "#evalKeyValue leaves values without $() untouched"</remarks>
    [Fact]
    public void TestEvalKeyValueLeavesValueWithoutSubstitutionUnchanged()
    {
        string result = Evaluate("HELLO", "World", new Dictionary<string, string>(), []);

        result.ShouldBe("World");
    }

    /// <remarks>Refer to "#evalKeyValue with multiple substitutions"</remarks>
    [Fact]
    public void TestEvalKeyValueSupportsMultipleSubstitutions()
    {
        string result = Evaluate("EVAL", "$(echo one),$(echo two)", new Dictionary<string, string>(), []);

        result.ShouldBe("one,two");
    }

    /// <remarks>Refer to "#evalKeyValue merges processEnv and runningParsed into the command's environment"</remarks>
    [Fact]
    public void TestEvalKeyValueMergesRunningParsedIntoEnvironment()
    {
        string command = IsWindows ? "$(echo %MY_VAR%)" : "$(echo $MY_VAR)";
        Dictionary<string, string> runningParsed = new() { ["MY_VAR"] = "fromRunningParsed" };

        string result = Evaluate("EVAL", command, new Dictionary<string, string>(), runningParsed);

        result.ShouldBe("fromRunningParsed");
    }

    /// <remarks>Refer to "#evalKeyValue with a failing command"</remarks>
    [Fact]
    public void TestEvalKeyValueThrowsCommandSubstitutionFailedOnNonZeroExit()
    {
        DotenvxException ex = Should.Throw<DotenvxException>(() =>
            Evaluate("EVAL", "$(exit 1)", new Dictionary<string, string>(), []));

        ex.Code.ShouldBe("COMMAND_SUBSTITUTION_FAILED");
    }
}
