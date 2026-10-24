using System.CommandLine;
using System.Text.Json;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/keypair.test.js</remarks>
public class KeyPairActionTest : EnvironmentTestBase
{
    private const string PublicKey = "03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba";
    private const string PrivateKey = "ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1";
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;

    public KeyPairActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new KeyPairAction(MockLogger) };
        // Every test here mocks KeyPair.Factory directly - the action's own missing-file check
        // (added 2026-09-25, see KeyPairAction.cs's class remarks) runs before that mock is ever
        // reached, so default to "file exists" here and cover the missing-file case explicitly.
        Fsx.ExistsSync = _ => true;
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    /// <remarks>Refer to "keypair"</remarks>
    [Fact]
    public void TestKeypair()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", "<PublicKey>" }, { "DOTENV_PRIVATE_KEY", "<PrivateKey>" }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        var data = new { DOTENV_PUBLIC_KEY = "<PublicKey>", DOTENV_PRIVATE_KEY = "<PrivateKey>", provider = (string?)null };
        output.ShouldBe($"{Helpers.JsonStringify(data)}\n");
    }

    /// <remarks>Refer to "keypair KEY"</remarks>
    [Fact]
    public void TestKeypairKey()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?> { { "DOTENV_PUBLIC_KEY", PublicKey } });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "DOTENV_PUBLIC_KEY"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"{PublicKey}\n");
    }


    /// <remarks>Refer to "keypair --format shell"</remarks>
    [Fact]
    public void TestKeypairFormatShell()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", PublicKey }, { "DOTENV_PRIVATE_KEY", PrivateKey }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "--format", "shell"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"DOTENV_PUBLIC_KEY={PublicKey} DOTENV_PRIVATE_KEY={PrivateKey}\n");
    }

    /// <remarks>Refer to "keypair --format shell (when null value should be empty string for shell format)"</remarks>
    [Fact]
    public void TestKeypairFormatShellWithNullValue()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", PublicKey }, { "DOTENV_PRIVATE_KEY", "" }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "--format", "shell"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"DOTENV_PUBLIC_KEY={PublicKey} DOTENV_PRIVATE_KEY=\n");
    }

    /// <remarks>Refer to "keypair --format colon"</remarks>
    [Fact]
    public void TestKeypairFormatColon()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", PublicKey }, { "DOTENV_PRIVATE_KEY", PrivateKey }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "--format", "colon"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"DOTENV_PUBLIC_KEY:{PublicKey} DOTENV_PRIVATE_KEY:{PrivateKey}\n");
    }

    /// <remarks>Refer to "keypair --format colon (when null value should be empty string for colon format)"</remarks>
    [Fact]
    public void TestKeypairFormatColonWithNullValue()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", PublicKey }, { "DOTENV_PRIVATE_KEY", null }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "--format", "colon"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"DOTENV_PUBLIC_KEY:{PublicKey} DOTENV_PRIVATE_KEY:\n");
    }

    /// <remarks>
    /// Refer to "keypair KEY --format colon". This test found that KeyPairAction.cs's single-key branch printed only the bare value
    /// regardless of --format, when keypair.js prints `${key}:${results}` specifically for
    /// key+colon - confirmed against the real JS source and fixed in KeyPairAction.cs.
    /// </remarks>
    [Fact]
    public void TestKeypairKeyFormatColon()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PRIVATE_KEY", PrivateKey }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "DOTENV_PRIVATE_KEY", "--format", "colon"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"DOTENV_PRIVATE_KEY:{PrivateKey}\n");
    }

    /// <remarks>Refer to "keypair --pretty-print"</remarks>
    [Fact]
    public void TestKeypairPrettyPrint()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", PublicKey }, { "DOTENV_PRIVATE_KEY", PrivateKey }
        });
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "--pretty-print"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        var data = new { DOTENV_PUBLIC_KEY = PublicKey, DOTENV_PRIVATE_KEY = PrivateKey, provider = (string?)null };
        output.ShouldBe($"{Helpers.JsonStringify(data, true)}\n");
    }

    /// <remarks>Refer to "keypair KEY (not found)"</remarks>
    [Fact]
    public void TestKeypairKeyNotFound()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns([]);
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "NOTFOUND"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        string output = _consoleOutput.ToString();
        output.ShouldBe("\n");
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-25 fix. Live-verified against Node: a
    /// missing env file exits 1 with a raw, untranslated ENOENT message (resolvers/keypair.js
    /// calls fsx.readFileX with no existence check at all, letting the raw fs error propagate
    /// through catchAndLog) - this port previously returned exit 0 with null keys instead.
    /// </remarks>
    [Fact]
    public void TestKeypairMissingEnvFile()
    {
        Fsx.ExistsSync = _ => false;

        ParseResult parseResult = RootCommand.Parse(["keypair"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(s =>
            s.StartsWith("ENOENT: no such file or directory, open '") && s.EndsWith(".env'"));
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-26 fix (see docs/porting-traps.md trap #30's "still-
    /// open gap" note). `resolvers/keypair.js`'s own for-loop merges each file's public/private key
    /// pair into one combined result object - this port previously only accepted a single `-f` file
    /// at all; confirmed live against Node that `-f .env -f .env.production` merges both files'
    /// keys into one output.
    /// </remarks>
    [Fact]
    public void TestKeypairMergesMultipleEnvFiles()
    {
        KeyPair mockDevKeyPair = Substitute.For<KeyPair>();
        mockDevKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY", PublicKey }, { "DOTENV_PRIVATE_KEY", PrivateKey }
        });
        KeyPair mockProductionKeyPair = Substitute.For<KeyPair>();
        mockProductionKeyPair.Run().Returns(new Dictionary<string, string?>
        {
            { "DOTENV_PUBLIC_KEY_PRODUCTION", PublicKey }, { "DOTENV_PRIVATE_KEY_PRODUCTION", PrivateKey }
        });
        KeyPair.Factory = (file, _) => file == ".env" ? mockDevKeyPair : mockProductionKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "-f", ".env", "-f", ".env.production"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        var data = new
        {
            DOTENV_PUBLIC_KEY = PublicKey,
            DOTENV_PRIVATE_KEY = PrivateKey,
            DOTENV_PUBLIC_KEY_PRODUCTION = PublicKey,
            DOTENV_PRIVATE_KEY_PRODUCTION = PrivateKey,
            provider = new Dictionary<string, string?> { [".env"] = null, [".env.production"] = null }
        };
        output.ShouldBe($"{Helpers.JsonStringify(data)}\n");
    }

    /// <remarks>
    /// Refer to "keypair --pp". The `--pp` shorthand for `--pretty-print`. No C# counterpart for "keypair updates spinner text while waiting for
    /// approval" and "keypair logs provider errors without an uncaught stack": the spinner and Armor providers are out of scope.
    /// </remarks>
    [Fact]
    public void TestKeypairPp()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?> { { "DOTENV_PUBLIC_KEY", "<publicKey>" } });
        KeyPair.Factory = (_, _) => mockKeyPair;

        int exitCode = RootCommand.Parse(["keypair", "--pp"]).Invoke();

        exitCode.ShouldBe(0);
        _consoleOutput.ToString().ShouldBe($"{Helpers.JsonStringify(new { DOTENV_PUBLIC_KEY = "<publicKey>", provider = (string?)null }, true)}\n");
    }
}
