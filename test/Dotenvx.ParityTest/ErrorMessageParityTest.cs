using System.Text.Json;
using System.Text.Json.Serialization;

using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Deterministic parity check for `dotenvx/src/lib/helpers/errors.js` - the single source of truth
/// for every error code/message/help text this port mirrors via `DotenvxException` and its ~20
/// subtypes (see that file's own doc comment). Rather than trust each subtype's hand-copied message
/// template to stay correct forever, this calls the REAL `Errors` class factories directly (the
/// live-oracle technique, here applied to a readable submodule file rather than the unreadable
/// `@dotenvx/primitives` package) with the same sample arguments used to construct the C# exception,
/// and compares `Code`/`Message`/`Help`/`MessageWithHelp` exactly.
///
/// Added 2026-10-02 as the next deterministic test in the same spirit as `HelpTextParityTest` -
/// closing off a whole bug class (drifted/fabricated error text) instead of relying on another
/// manual sweep. It immediately found one: `Conventions.cs`'s invalid-`--convention` branch threw a
/// plain `System.Exception` with a fabricated message instead of the real `INVALID_CONVENTION`
/// error (fixed alongside this test, see `InvalidConventionException`).
///
/// Not every `errors.js` factory has a C# equivalent to test here - several are genuinely
/// unreachable or out of scope for this port: `commandExitedWithCode` (thrown internally by
/// `executeCommand.js` but deliberately never surfaced to the user - see trap for `ExecuteCommand.cs`'s
/// own suppression), `onePasswordFailed`/`bitwardenFailed` (1Password/Bitwarden secret resolution,
/// out of scope), `invalidColor`/`missingLogLevel` (internal safety checks against hardcoded,
/// always-valid call sites, not reachable from user input), `invalidPassphrase` (the `lock`/
/// passphrase feature, out of scope), and `invalidPublicKey`/`missingPublicKey` (confirmed via grep
/// to have zero call sites anywhere in the current pinned source - dead code upstream, nothing to
/// port). `custom()` is also excluded - its message/help are caller-supplied, not a fixed template.
/// </remarks>
public class ErrorMessageParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const string SamplePrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";
    private const string SamplePublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";
    private const string SamplePublicKey2 = "03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba";

    private sealed record JsErrorResult(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("help")] string? Help,
        [property: JsonPropertyName("messageWithHelp")] string MessageWithHelp);

    [Fact]
    public async Task EveryErrorFactoryMatchesRealUpstream()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        List<(string Method, object Args, DotenvxException Exception)> cases =
        [
            ("commandSubstitutionFailed",
                new { key = "KEY", command = "exit 1", message = "boom" },
                new CommandSubstitutionFailed("KEY", "exit 1", "boom")),
            ("decryptionFailed",
                new { message = "could not decrypt KEY" },
                new DecryptionFailedException("could not decrypt KEY")),
            ("invalidPrivateKey",
                new { key = "KEY", privateKeyName = "DOTENV_PRIVATE_KEY", privateKey = SamplePrivateKey },
                new InvalidPrivateKeyException("KEY", "DOTENV_PRIVATE_KEY", SamplePrivateKey)),
            ("malformedEncryptedData",
                new { key = "KEY" },
                new MalformedEncryptedDataException("KEY")),
            ("mispairedPrivateKey",
                new { publicKey = SamplePublicKey, publicKeyExisting = SamplePublicKey2 },
                new MispairedPrivateKeyException(SamplePublicKey, SamplePublicKey2)),
            ("missingDirectory",
                new { directory = "/some/dir" },
                new MissingDirectoryException("/some/dir")),
            ("missingEnvFile",
                new { envFilepath = ".env.production" },
                new MissingEnvFileException(".env.production")),
            ("missingEnvExample",
                new { },
                new MissingEnvExampleException()),
            ("missingEnvKeysFile",
                new { envKeysFilepath = ".env.keys" },
                new MissingEnvKeysFileException(".env.keys")),
            ("missingEnvFiles",
                new { },
                new MissingEnvFilesException()),
            ("missingKey",
                new { key = "KEY" },
                new MissingKeyException("KEY")),
            ("missingPrivateKey",
                new { key = "KEY", privateKeyName = "DOTENV_PRIVATE_KEY", privateKey = SamplePrivateKey },
                new MissingPrivateKeyException("KEY", "DOTENV_PRIVATE_KEY", SamplePrivateKey)),
            ("fileNotWritable",
                new { filepath = "/some/file" },
                new FileNotWritableException("/some/file")),
            ("missingValue",
                new { key = "KEY" },
                new MissingValueException("KEY")),
            ("validationFailed",
                new { message = "missing required (KEY)" },
                new ValidationFailedException("missing required (KEY)")),
            ("precommitHookModifyFailed",
                new { error = new { message = "disk full" } },
                new PrecommitHookModifyFailedException("disk full")),
            ("wrongPrivateKey",
                new { key = "KEY", privateKeyName = "DOTENV_PRIVATE_KEY", privateKey = SamplePrivateKey },
                new WrongPrivateKeyException("KEY", "DOTENV_PRIVATE_KEY", SamplePrivateKey)),
            ("invalidConvention",
                new { convention = "bogus" },
                new InvalidConventionException("bogus"))
        ];

        string script = BuildScript(cases);
        CliResult result = await Fixture.RunNodeScriptAsync(script);
        result.ExitCode.ShouldBe(0, $"Node script failed:\n{result.StandardError}");

        List<JsErrorResult> nodeResults = JsonSerializer.Deserialize<List<JsErrorResult>>(result.StandardOutput)!;
        nodeResults.Count.ShouldBe(cases.Count);

        List<string> mismatches = [];
        for (int i = 0; i < cases.Count; i++)
        {
            (string method, _, DotenvxException actual) = cases[i];
            JsErrorResult expected = nodeResults[i];

            if (actual.Code != expected.Code)
            {
                mismatches.Add($"{method}: Code '{actual.Code}' != Node '{expected.Code}'");
            }

            if (actual.Message != expected.Message)
            {
                mismatches.Add($"{method}: Message '{actual.Message}' != Node '{expected.Message}'");
            }

            if ((actual.Help ?? "") != (expected.Help ?? ""))
            {
                mismatches.Add($"{method}: Help '{actual.Help}' != Node '{expected.Help}'");
            }

            if (actual.MessageWithHelp != expected.MessageWithHelp)
            {
                mismatches.Add(
                    $"{method}: MessageWithHelp '{actual.MessageWithHelp}' != Node '{expected.MessageWithHelp}'");
            }
        }

        mismatches.ShouldBeEmpty(string.Join('\n', mismatches));
    }

    private static string BuildScript(List<(string Method, object Args, DotenvxException Exception)> cases)
    {
        var jsCases = cases.Select(c => new { method = c.Method, args = c.Args });
        string json = JsonSerializer.Serialize(jsCases);

        return $$"""
            const Errors = require('./src/lib/helpers/errors.js');
            const cases = {{json}};
            const results = cases.map(c => {
              const e = new Errors(c.args)[c.method]();
              return { code: e.code, message: e.message, help: e.help, messageWithHelp: e.messageWithHelp };
            });
            console.log(JSON.stringify(results));
            """;
    }
}
