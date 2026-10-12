using System.Text.RegularExpressions;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - upstream has no tests for `lock up`/`lock down`
/// (dotenvx/src/cli/actions/lock/*.js). Both CLIs run against the same fixtures with the passphrase piped to stdin;
/// salts and IVs are random, so locked payloads are redacted and compatibility is proven by cross-unlocking.
/// </remarks>
public partial class LockParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    [GeneratedRegex(@"locked:([0-9a-f]+):[A-Za-z0-9_-]+")]
    private static partial Regex LockedRegex();

    private static string Redact(string text)
    {
        return LockedRegex().Replace(text, "locked:$1:<payload>");
    }

    private void Setup()
    {
        WriteFile(".env", $"DOTENV_PUBLIC_KEY=\"{FixedKeypairFixture.PublicKey}\"\nA=\"encrypted:xyz\"\n");
        WriteFile(".env.keys", FixedKeypairFixture.PrivateKeyFile);
    }

    [Fact]
    public async Task LockUpMatchesNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string? nodeKeys = null;
        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(Setup, null, "pw\n", "lock", "up");
        // the shared WorkDir holds the .NET run's files now; re-run Node alone to capture its keys file
        Setup();
        await Fixture.RunNodeWithInputAsync(WorkDir, null, "pw\n", "lock", "up");
        nodeKeys = ReadFile(".env.keys");
        Setup();
        await Fixture.RunDotnetWithInputAsync(WorkDir, null, "pw\n", "lock", "up");
        string dotnetKeys = ReadFile(".env.keys");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        Redact(dotnetKeys).ShouldBe(Redact(nodeKeys));
        dotnetKeys.ShouldContain("DOTENV_PRIVATE_KEY=locked:" + FixedKeypairFixture.PublicKey + ":");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LockedByOneCliUnlocksWithTheOther(bool nodeLocks)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        Setup();
        CliResult up = nodeLocks
            ? await Fixture.RunNodeWithInputAsync(WorkDir, null, "pässword 🔑\n", "lock", "up")
            : await Fixture.RunDotnetWithInputAsync(WorkDir, null, "pässword 🔑\n", "lock", "up");
        up.ExitCode.ShouldBe(0);
        ReadFile(".env.keys").ShouldContain("locked:");

        CliResult down = nodeLocks
            ? await Fixture.RunDotnetWithInputAsync(WorkDir, null, "pässword 🔑\n", "lock", "down")
            : await Fixture.RunNodeWithInputAsync(WorkDir, null, "pässword 🔑\n", "lock", "down");

        down.ExitCode.ShouldBe(0);
        ReadFile(".env.keys").ShouldBe(FixedKeypairFixture.PrivateKeyFile);
    }

    [Fact]
    public async Task LockDownWithWrongPassphraseMatchesNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string locked = "";
        Setup();
        await Fixture.RunNodeWithInputAsync(WorkDir, null, "pw\n", "lock", "up");
        locked = ReadFile(".env.keys");

        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(() =>
        {
            Setup();
            WriteFile(".env.keys", locked);
        }, null, "bad\n", "lock", "down");

        node.ExitCode.ShouldBe(1);
        dotnet.ExitCode.ShouldBe(1);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        node.StandardError.ShouldContain("[INVALID_PASSPHRASE] could not unlock DOTENV_PRIVATE_KEY using passphrase");
        dotnet.StandardError.ShouldContain("[INVALID_PASSPHRASE] could not unlock DOTENV_PRIVATE_KEY using passphrase");
        ReadFile(".env.keys").ShouldBe(locked);
    }

    [Theory]
    [InlineData("up", "")]
    [InlineData("down", "")]
    [InlineData("up", "no newline")]
    public async Task InputEndingBeforeEnterExitsSilentlyLikeNode(string direction, string input)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(Setup, null, input, "lock", direction);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        ReadFile(".env.keys").ShouldBe(FixedKeypairFixture.PrivateKeyFile);
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    public async Task NothingToDoPrintsNoChangeWithoutPrompting(string direction)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        // `down` on an unlocked key and (after a lock) `up` on a locked key never prompt
        string keys = FixedKeypairFixture.PrivateKeyFile;
        if (direction == "up")
        {
            Setup();
            await Fixture.RunNodeWithInputAsync(WorkDir, null, "pw\n", "lock", "up");
            keys = ReadFile(".env.keys");
        }

        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(() =>
        {
            Setup();
            WriteFile(".env.keys", keys);
        }, null, "", "lock", direction);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        dotnet.StandardOutput.ShouldContain("○ no change (02B 106)");
    }

    [Theory]
    [InlineData("up", ".env.keys")]
    [InlineData("down", ".env.keys")]
    [InlineData("up", ".env")]
    public async Task MissingFilesAndKeysMatchNode(string direction, string missing)
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(() =>
        {
            Setup();
            File.Delete(Path.Combine(WorkDir, missing));
        }, null, "pw\n", "lock", direction);

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        // node prints the raw ENOENT text ("ENOENT: no such file or directory, open '...'"): only the leading code is compared
        dotnet.StandardError.Contains("ENOENT").ShouldBe(node.StandardError.Contains("ENOENT"));
    }

    [Fact]
    public async Task NoMatchingPrivateKeyMatchesNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(() =>
        {
            Setup();
            WriteFile(".env.keys", "OTHER=1\n");
        }, null, "pw\n", "lock", "up");

        node.StandardError.ShouldContain("[MISSING_KEY] missing key (DOTENV_PRIVATE_KEY)");
        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardError.ShouldBe(node.StandardError);
    }

    [Fact]
    public async Task LockWithoutSubcommandPrintsHelp()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothAsync(() => { }, null, "lock");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldContain("up");
        dotnet.StandardOutput.ShouldContain("down");
    }

    [Fact]
    public async Task LockFilePathOptionsMatchNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        (CliResult node, CliResult dotnet) = await RunBothWithInputAsync(() =>
        {
            WriteFile("sub/.env", $"DOTENV_PUBLIC_KEY=\"{FixedKeypairFixture.PublicKey}\"\nA=\"encrypted:xyz\"\n");
            WriteFile("other.keys", FixedKeypairFixture.PrivateKeyFile);
        }, null, "pw\n", "lock", "up", "-f", "sub/.env", "-fk", "other.keys");

        dotnet.ExitCode.ShouldBe(node.ExitCode);
        dotnet.StandardOutput.ShouldBe(node.StandardOutput);
        ReadFile("other.keys").ShouldContain("DOTENV_PRIVATE_KEY=locked:");
    }
}
