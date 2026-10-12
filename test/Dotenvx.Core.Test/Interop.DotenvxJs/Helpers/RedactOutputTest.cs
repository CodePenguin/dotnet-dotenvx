using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/redactOutput.test.js. Besides the faithful recursive RedactOutput,
/// the string/dictionary conveniences and the chunked stream writer holdback buffering are covered (see RedactOutput.cs class-
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

    /// <remarks>Refer to "createRedactedStreamWriter redacts values split across chunks"</remarks>
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

    /// <remarks>
    /// Refer to "createRedactedStreamWriter passes through unrelated output immediately". There is no C# counterpart for
    /// "createRedactedStreamWriter pauses and resumes for backpressure": the source stream is read by a loop that only
    /// reads the next chunk after the previous one was written (see RedactOutput.cs).
    /// </remarks>
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

    /// <remarks>Refer to "redactOutput replaces sensitive values in strings, arrays, and plain objects"</remarks>
    [Fact]
    public void RedactOutputReplacesSensitiveValuesInStringsArraysAndPlainObjects()
    {
        string[] sensitiveValues = ["super-secret", "secret"];

        RedactOutput("value=super-secret", sensitiveValues).ShouldBe("value=[REDACTED]");
        RedactOutput(new object[] { "secret", new Dictionary<string, object> { ["nested"] = "super-secret" } },
                sensitiveValues)
            .ShouldBeOfType<List<object?>>()
            .ShouldBe(["[REDACTED]", new Dictionary<string, object?> { ["nested"] = "[REDACTED]" }]);
        RedactOutput("public-value", sensitiveValues).ShouldBe("public-value");
    }

    /// <remarks>Refer to "createRedactedStreamWriter does not flush a partial secret while waiting for another chunk"</remarks>
    [Fact]
    public void StreamWriterDoesNotFlushAPartialSecretWhileWaitingForAnotherChunk()
    {
        StringWriter output = new();
        RedactedStreamWriter writer = new(output, ["super-secret"]);

        writer.Write("value=super-");
        output.ToString().ShouldBe("value=");

        writer.Write("secret\n");
        writer.Flush();

        output.ToString().ShouldBe("value=[REDACTED]\n");
    }

    /// <remarks>
    /// Refer to "createRedactedStreamWriter redacts a unicode value split inside a character". Upstream splits a
    /// UTF-8 byte stream mid-character; .NET already decodes bytes to text, so the equivalent split here is
    /// between the two UTF-16 halves of the surrogate pair in the emoji.
    /// </remarks>
    [Fact]
    public void StreamWriterRedactsAUnicodeValueSplitInsideACharacter()
    {
        StringWriter output = new();
        RedactedStreamWriter writer = new(output, ["key-🔑-secret"]);
        string value = "value=key-🔑-secret\n";
        int splitAt = "value=key-".Length + 1;

        writer.Write(value[..splitAt]);
        writer.Write(value[splitAt..]);
        writer.Flush();

        output.ToString().ShouldBe("value=[REDACTED]\n");
    }

}
