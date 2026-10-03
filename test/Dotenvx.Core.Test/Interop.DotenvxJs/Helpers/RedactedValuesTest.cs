using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/redactedValues.test.js (one upstream test; the others are extra
/// scenarios mirroring the source filtering logic directly: skip
/// `_PLAIN`-suffixed keys, skip null/empty values, dedupe).
/// </remarks>
public class RedactedValuesTest
{
    [Fact]
    public void CollectsInjectedAndPreExistedValues()
    {
        RunProcessedEnv[] processedEnvs =
        [
            new RunProcessedEnv
            {
                Injected = new Dictionary<string, string> { ["HELLO"] = "World" },
                PreExisted = new Dictionary<string, string> { ["FOO"] = "bar" }
            }
        ];

        RedactedValues(processedEnvs).ShouldBe(["World", "bar"], ignoreOrder: true);
    }

    [Fact]
    public void ExcludesPlainSuffixedKeys()
    {
        RunProcessedEnv[] processedEnvs =
        [
            new RunProcessedEnv
            {
                Injected = new Dictionary<string, string> { ["HELLO_PLAIN"] = "NotSecret", ["HELLO"] = "World" }
            }
        ];

        RedactedValues(processedEnvs).ShouldBe(["World"]);
    }

    [Fact]
    public void ExcludesEmptyValues()
    {
        RunProcessedEnv[] processedEnvs =
        [
            new RunProcessedEnv { Injected = new Dictionary<string, string> { ["HELLO"] = "" } }
        ];

        RedactedValues(processedEnvs).ShouldBeEmpty();
    }

    [Fact]
    public void DedupesRepeatedValuesAcrossProcessedEnvs()
    {
        RunProcessedEnv[] processedEnvs =
        [
            new RunProcessedEnv { Injected = new Dictionary<string, string> { ["A"] = "same" } },
            new RunProcessedEnv { Injected = new Dictionary<string, string> { ["B"] = "same" } }
        ];

        RedactedValues(processedEnvs).ShouldBe(["same"]);
    }

    /// <remarks>Refer to "redactedValues returns injected values except _PLAIN values"</remarks>
    [Fact]
    public void ReturnsInjectedValuesExceptPlainValues()
    {
        RunProcessedEnv[] processedEnvs =
        [
            new RunProcessedEnv
            {
                Injected = new Dictionary<string, string>
                {
                    ["SECRET"] = "super-secret", ["PUBLIC"] = "public-value", ["VISIBLE_PLAIN"] = "visible-value",
                    ["EMPTY"] = ""
                }
            },
            new RunProcessedEnv
            {
                Injected = new Dictionary<string, string> { ["INLINE"] = "inline-value" },
                PreExisted = new Dictionary<string, string>
                {
                    ["EXISTING"] = "external-value", ["EXISTING_PLAIN"] = "external-visible-value"
                }
            }
        ];

        RedactedValues(processedEnvs).ShouldBe(["super-secret", "public-value", "inline-value", "external-value"]);
    }
}
