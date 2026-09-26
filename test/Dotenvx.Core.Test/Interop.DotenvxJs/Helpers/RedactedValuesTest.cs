using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactedValues.js. No dedicated upstream unit-test
/// file exists for this helper - scenarios mirror the source's own filtering logic directly (skip
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
}
