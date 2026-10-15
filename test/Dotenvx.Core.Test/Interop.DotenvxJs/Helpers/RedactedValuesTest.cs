using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactedValues.js (no upstream test file; these scenarios mirror the source
/// directly): nothing is redacted without an Envfile unless the legacy `--redact` rule is on; the legacy rule redacts every
/// injected/pre-existing value except the ones of public keys; with an Envfile each key's own policy decides.
/// </remarks>
public class RedactedValuesTest
{
    private static RunProcessedEnv Row(Dictionary<string, string> injected, Dictionary<string, string>? preExisted = null)
    {
        return new RunProcessedEnv { Injected = injected, PreExisted = preExisted ?? [] };
    }

    private static EnvfileSchema Schema(params (string Key, bool Redacted)[] keys)
    {
        EnvfileSchema schema = new()
        {
            Exists = true,
            Keys = keys.ToDictionary(key => key.Key,
                key => new global::Dotenvx.Primitives.KeyPolicy { Redacted = key.Redacted })
        };

        return schema;
    }

    [Fact]
    public void ReturnsNothingWithoutAnEnvfileOrLegacyRedact()
    {
        RedactedValues([Row(new Dictionary<string, string> { ["HELLO"] = "World" })]).ShouldBeEmpty();
    }

    [Fact]
    public void LegacyCollectsInjectedAndPreExistedValues()
    {
        RunProcessedEnv[] processedEnvs =
            [Row(new Dictionary<string, string> { ["HELLO"] = "World" }, new Dictionary<string, string> { ["FOO"] = "bar" })];

        RedactedValues(processedEnvs, legacyRedact: true).ShouldBe(["World", "bar"], ignoreOrder: true);
    }

    [Fact]
    public void LegacySkipsPublicKeyValues()
    {
        RunProcessedEnv[] processedEnvs =
        [
            Row(new Dictionary<string, string>
            {
                ["DOTENV_PUBLIC_KEY"] = "pub", ["VITE_URL"] = "vite", ["HELLO"] = "World"
            })
        ];

        RedactedValues(processedEnvs, legacyRedact: true).ShouldBe(["World"]);
    }

    [Fact]
    public void ExcludesEmptyValues()
    {
        RedactedValues([Row(new Dictionary<string, string> { ["HELLO"] = "" })], legacyRedact: true).ShouldBeEmpty();
    }

    [Fact]
    public void DedupesRepeatedValuesAcrossProcessedEnvs()
    {
        RunProcessedEnv[] processedEnvs =
        [
            Row(new Dictionary<string, string> { ["A"] = "same" }), Row(new Dictionary<string, string> { ["B"] = "same" })
        ];

        RedactedValues(processedEnvs, legacyRedact: true).ShouldBe(["same"]);
    }

    [Fact]
    public void EnvfilePolicyDecidesPerKey()
    {
        RunProcessedEnv[] processedEnvs =
        [
            Row(new Dictionary<string, string> { ["SECRET"] = "s", ["OPEN"] = "o", ["UNLISTED"] = "u" })
        ];

        RedactedValues(processedEnvs, Schema(("SECRET", true), ("OPEN", false))).ShouldBe(["s", "u"], ignoreOrder: true);
    }

    [Fact]
    public void EnvfileKeysOnlyInTheEnvironmentAreRedacted()
    {
        RedactedValues([], Schema(("SHELL_ONLY", true), ("OPEN", false)),
            new Dictionary<string, string> { ["SHELL_ONLY"] = "from-shell", ["OPEN"] = "open" }).ShouldBe(["from-shell"]);
    }
}
