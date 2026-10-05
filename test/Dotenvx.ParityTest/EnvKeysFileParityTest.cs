using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Cross-implementation coverage for the multi-value `-fk`/`--env-keys-file` flag and for
/// `dotenvx/src/lib/helpers/resolveEnvKeysFile.js`'s directory resolution, which upstream applies to
/// only SOME commands. `dotenvx.js` wires `-fk` two different ways and this class pins both:
///
/// - `collectEnvKeys` (accumulates every occurrence into a list): `run`, `get`, `validate`, `decrypt`,
///   `keypair`. A port that kept only the first `-fk` silently loses the key when the real
///   `.env.keys` isn't first - `KeysFor`/`KeyPair`/`Get`/`Run` all take a list.
/// - a plain `&lt;path&gt;` string (commander keeps the LAST occurrence): `set`, `encrypt`, `del`.
///   `SetAction`/`EncryptAction`/`RotateAction` unwrap with `.LastOrDefault()`.
///
/// On top of the list itself, only `run`/`get`/`validate` call `resolveEnvKeysFile`, so
/// `-fk <directory>` means `&lt;directory&gt;/.env.keys` there and stays a literal (nonexistent) path
/// in `decrypt`/`keypair`. The paired Theory rows below are the discriminator: the same directory
/// argument must succeed in one command and fail in the other.
///
/// No upstream JS test titles these scenarios directly at this pin, hence "Not from a JS test".
/// </remarks>
public class EnvKeysFileParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    // A syntactically valid but WRONG secp256k1 private key. It must parse, so that using it fails
    // with DECRYPTION_FAILED (a clean, comparable outcome) instead of a keys-file parse error - that
    // is what makes "decoy first, real second" a real discriminator.
    private const string DecoyKeyFile = "DOTENV_PRIVATE_KEY=ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1\n";

    /// <summary>
    /// An encrypted `.env` whose only usable private key lives in `real/.env.keys`, with a decoy
    /// `.env.keys` in `decoy/`. Deliberately NO `.env.keys` at the working-directory root, so nothing
    /// can fall back to a default keyring and mask a resolution/ordering bug. `.env.example` is only
    /// read by `validate`, where it makes "decrypted and validated" (exit 0) the expected outcome.
    /// </summary>
    private void WriteSplitKeyringFixture()
    {
        WriteFile(".env", FixedKeypairFixture.EncryptedEnv);
        WriteFile(".env.example", "HELLO=\n");
        WriteFile("decoy/.env.keys", DecoyKeyFile);
        WriteFile("real/.env.keys", FixedKeypairFixture.PrivateKeyFile);
    }

    /// <summary>
    /// The core multi-value regression: only a port that collects BOTH `-fk` occurrences can reach the
    /// real key. A first-only or last-only port prints a DECRYPTION_FAILED error here instead.
    /// </summary>
    [Fact]
    public async Task GetReadsTheRealKeyFromTheSecondOfTwoKeysFiles()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteSplitKeyringFixture, null,
            "get", "-fk", "decoy/.env.keys", "-fk", "real/.env.keys", "HELLO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnet.ExitCode.ShouldBe(0);
    }

    /// <summary>Ordering must not matter: commander appends, it does not replace.</summary>
    [Fact]
    public async Task GetReadsTheRealKeyFromTheFirstOfTwoKeysFiles()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteSplitKeyringFixture, null,
            "get", "-fk", "real/.env.keys", "-fk", "decoy/.env.keys", "HELLO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnet.ExitCode.ShouldBe(0);
    }

    /// <summary>
    /// `-fk` is `-fk, --env-keys-file &lt;path&gt;` upstream - ONE value per occurrence, NOT a variadic
    /// like `-e, --env &lt;strings...&gt;`. So `get -fk a b` binds only `a`, and `b` becomes the positional
    /// KEY (reported as MISSING_KEY), rather than being collected as a second keys file. This is the
    /// binding-side half of the same distinction <c>EnvItemOrdering</c> has to honor on the token-walk
    /// side, and it is what stops a space-separated list from silently working on this port alone.
    /// </summary>
    [Fact]
    public async Task GetKeysFileTakesOnlyOneValuePerOccurrence()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteSplitKeyringFixture, null,
            "get", "-fk", "decoy/.env.keys", "real/.env.keys");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnet.ExitCode.ShouldBe(1);
    }

    /// <summary>Missing paths are preserved verbatim by resolveEnvKeysFile, so they fail later.</summary>
    [Fact]
    public async Task GetMissingKeysFilePathMatches()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteSplitKeyringFixture, null,
            "get", "-fk", "nosuchdir", "-fk", "real/.env.keys", "HELLO");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnet.ExitCode.ShouldBe(0);
    }

    /// <summary>
    /// `run`/`get`/`validate` resolve `-fk &lt;directory&gt;` to `&lt;directory&gt;/.env.keys`; `decrypt`/
    /// `keypair` deliberately do NOT, so the very same argument must resolve in one command and be
    /// reported as a missing file in the other. Both halves of each pair are asserted against Node.
    /// </summary>
    [Theory]
    [InlineData("get", "-fk", "real", "HELLO", 0)]
    [InlineData("get", "-fk", "real/.env.keys", "HELLO", 0)]
    [InlineData("validate", "-fk", "real", "", 0)]
    [InlineData("validate", "-fk", "real/.env.keys", "", 0)]
    [InlineData("validate", "-fk", "nosuchdir", "", 1)]
    // keypair does not resolve a directory: `real` is read as a literal file, which doesn't exist.
    [InlineData("keypair", "-fk", "real", "DOTENV_PRIVATE_KEY", 0)]
    [InlineData("keypair", "-fk", "real/.env.keys", "DOTENV_PRIVATE_KEY", 0)]
    // decrypt does not resolve a directory either, so the decoy-free directory fails to decrypt.
    [InlineData("decrypt", "-fk", "real", "--stdout", 1)]
    [InlineData("decrypt", "-fk", "real/.env.keys", "--stdout", 0)]
    public async Task KeysFileDirectoryResolutionMatchesPerCommand(string command, string flag, string value,
        string trailingArg, int expectedExitCode)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] args = trailingArg.Length > 0 ? [command, flag, value, trailingArg] : [command, flag, value];

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteSplitKeyringFixture, null, args);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        node.ExitCode.ShouldBe(expectedExitCode);
    }

    /// <summary>Same, for `run`, which must resolve before injecting and then exec the child.</summary>
    [Fact]
    public async Task RunResolvesKeysFileDirectory()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(WriteSplitKeyringFixture, null,
            "run", "-fk", "decoy", "-fk", "real", "--", "node", "-e", "console.log(process.env.HELLO)");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardError.ShouldBe(node.StandardError);
        dotnet.ExitCode.ShouldBe(0);
    }

    /// <remarks>
    /// Not from a JS test - found by code review: `set`/`encrypt` register `-fk` as a plain `&lt;path&gt;` option, so
    /// commander keeps the LAST occurrence (the keys file that gets written), and `set` also accepts unknown options.
    /// </remarks>
    [Fact]
    public async Task EncryptWritesTheKeysFileOfTheLastFkOccurrence()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => WriteFile(".env", "A=1\n"), null,
            "encrypt", "-fk", "one.keys", "-fk", "two.keys");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        File.Exists(Path.Combine(WorkDir, "two.keys")).ShouldBeTrue();
        File.Exists(Path.Combine(WorkDir, "one.keys")).ShouldBeFalse();
    }

    [Fact]
    public async Task SetAcceptsUnknownOptionsAndDashLeadingValues()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => WriteFile(".env", ""), null,
            "set", "A", "-secret", "--whatever", "--plain");

        node.ExitCode.ShouldBe(0);
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        ReadFile(".env").ShouldContain("A=\"-secret\"");
    }
}
