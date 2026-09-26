using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactOutput.js. Only the string/dictionary overloads
/// and the chunked stream writer's holdback buffering are ported (see RedactOutput.cs's own class-
/// level remark for why the array/object recursion and byte-level StringDecoder aren't) - scenarios
/// below cover those, plus the streaming holdback logic's actual reason for existing: never emitting
/// a sensitive value that's split across two separate write chunks.
/// </remarks>
public class RedactOutputTest
{
    [Fact]
    public void RedactsMatchingSubstring()
    {
        RedactOutputText("value: World", ["World"]).ShouldBe("value: [REDACTED]");
    }

    [Fact]
    public void NoSensitiveValuesReturnsOriginal()
    {
        RedactOutputText("value: World", []).ShouldBe("value: World");
    }

    [Fact]
    public void NullValueReturnsNull()
    {
        RedactOutputText(null, ["World"]).ShouldBeNull();
    }

    [Fact]
    public void RedactsLongestValueFirstToAvoidPartialOverlap()
    {
        RedactOutputText("WorldWide", ["World", "WorldWide"]).ShouldBe("[REDACTED]");
    }

    [Fact]
    public void RedactOutputDictRedactsEachValue()
    {
        Dictionary<string, string> result = RedactOutputDict(
            new Dictionary<string, string> { ["HELLO"] = "World", ["FOO"] = "bar" }, ["World"]);

        result.ShouldBe(new Dictionary<string, string> { ["HELLO"] = "[REDACTED]", ["FOO"] = "bar" });
    }

    [Fact]
    public void StreamWriterHoldsBackAndRedactsValueSplitAcrossChunks()
    {
        StringWriter output = new();
        RedactedStreamWriter writer = new(output, ["SuperSecret"]);

        writer.Write("value: Super");
        writer.Write("Secret!");
        writer.Flush();

        output.ToString().ShouldBe("value: [REDACTED]!");
    }

    [Fact]
    public void StreamWriterPassesThroughNonMatchingChunksImmediately()
    {
        StringWriter output = new();
        RedactedStreamWriter writer = new(output, ["SuperSecret"]);

        writer.Write("hello ");
        writer.Write("world");
        writer.Flush();

        output.ToString().ShouldBe("hello world");
    }
}
