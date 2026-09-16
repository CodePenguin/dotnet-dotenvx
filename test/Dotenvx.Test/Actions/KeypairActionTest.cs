using System.CommandLine;
using System.Text.Json;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/keypair.test.js</remarks>
public class KeyPairActionTest : EnvironmentTestBase
{
    private const string PublicKey = "03eaf2142ab3d55bdf108962334e06696db798e7412cfc51d75e74b4f87f299bba";
    private const string PrivateKey = "ec9e80073d7ace817d35acb8b7293cbf8e5981b4d2f5708ee5be405122993cd1";
    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;
    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true };

    public KeyPairActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new KeyPairAction(MockLogger) };
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
        var data = new { DOTENV_PUBLIC_KEY = "<PublicKey>", DOTENV_PRIVATE_KEY = "<PrivateKey>" };
        output.ShouldBe($"{JsonSerializer.Serialize(data, _compactJsonOptions)}\n");
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
        var data = new { DOTENV_PUBLIC_KEY = PublicKey, DOTENV_PRIVATE_KEY = PrivateKey };
        output.ShouldBe($"{JsonSerializer.Serialize(data, _prettyPrintJsonOptions)}\n");
    }

    /// <remarks>Refer to "keypair KEY (not found)"</remarks>
    [Fact]
    public void TestKeypairKeyNotFound()
    {
        KeyPair mockKeyPair = Substitute.For<KeyPair>();
        mockKeyPair.Run().Returns(new Dictionary<string, string?>());
        KeyPair.Factory = (_, _) => mockKeyPair;

        ParseResult parseResult = RootCommand.Parse(["keypair", "NOTFOUND"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        string output = _consoleOutput.ToString();
        output.ShouldBe("\n");
    }
}