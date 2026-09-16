using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/encrypt.test.js</remarks>
public class EncryptActionTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;

    public EncryptActionTest()
    {
        File.WriteAllText(".gitignore", ".env.keys");
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new EncryptAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
        File.Delete(".gitignore");
    }

    /// <remarks>Refer to "encrypt - nothing"</remarks>
    [Fact]
    public void TestEncryptNothing()
    {
        File.WriteAllText(".env", "");
        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"no changes .env ({Path.GetFullPath(".env")})");
    }

    /// <remarks>Refer to "enc" shorthand alias for "encrypt" (v2.21.0)</remarks>
    [Fact]
    public void TestEncAliasRunsEncrypt()
    {
        File.WriteAllText(".env", "");
        ParseResult parseResult = RootCommand.Parse(["enc"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
    }

    /// <remarks>Refer to "encrypt - .env but no changes"</remarks>
    [Fact]
    public void TestEncryptEnvButNoChanges()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env.keys", ".env.keys");
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env");

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("no changes (.env)");
    }

    /// <remarks>Refer to "encrypt - --stdout"</remarks>
    [Fact]
    public void TestEncryptStdout()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env.keys", ".env.keys");
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env");
        string envFileContents = File.ReadAllText(".env");

        ParseResult parseResult = RootCommand.Parse(["encrypt", "--stdout"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"{envFileContents}\n");
    }

    /// <remarks>Refer to "encrypt - .env with changes"</remarks>
    [Fact]
    public void TestEncryptEnvWithChanges()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env.keys", ".env.keys");
        File.WriteAllText(".env", "HELLO=World");

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"encrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("✔ encrypted (.env)");
    }

    /// <remarks>Refer to "encrypt - .env with changes and privateKeyAdded"</remarks>
    [Fact]
    public void TestEncryptEnvWithChangesAndPrivateKeyAdded()
    {
        File.WriteAllText(".env", "HELLO=World");

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"encrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("✔ encrypted (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
        MockLogger.InfoMessages.ShouldContain(s =>
            s.StartsWith("⮕  next run [DOTENV_PRIVATE_KEY='") &&
            s.EndsWith("' dotenvx run -- yourcommand] to test decryption locally"));
    }

    /// <remarks>Refer to "encrypt - .env with changes and privateKeyAdded but not ignoring .env.keys"</remarks>
    [Fact]
    public void TestEncryptEnvWithChangesAndPrivateKeyAddedButNotIgnoringEnvKeys()
    {
        File.Delete(".gitignore");
        File.WriteAllText(".env", "HELLO=World");

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"encrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("✔ encrypted (.env)");
        MockLogger.InfoMessages.ShouldContain("✔ key added to .env.keys (DOTENV_PRIVATE_KEY)");
        MockLogger.InfoMessages.ShouldContain(s =>
            s.StartsWith("⮕  next run [DOTENV_PRIVATE_KEY='") &&
            s.EndsWith("' dotenvx run -- yourcommand] to test decryption locally"));
        MockLogger.InfoMessages.ShouldContain(
            "⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
    }

    /// <remarks>Refer to "encrypt - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestEncryptMissingEnvFile()
    {
        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.WarningMessages.ShouldContain($"[MISSING_ENV_FILE] missing .env file ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain(
            "? add one with [echo \"HELLO=World\" > .env] and re-run [dotenvx encrypt]");
    }

    /// <remarks>Refer to "encrypt - OTHER_ERROR"</remarks>
    [Fact]
    public void TestEncryptOtherError()
    {
        // Simulate an error within the Decrypt.Run
        using FileStream fs = new(".env", FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(""u8);

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.WarningMessages.ShouldContain(s => s.StartsWith("The process cannot access the file"));
    }

    /// <remarks>Refer to "encrypt - catch error"</remarks>
    [Fact]
    public void TestEncryptCatchError()
    {
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        MockLogger.BeforeLog = message =>
        {
            if (message.StartsWith("encrypting"))
            {
                throw new DotenvxException("500", "Mock Error", "Mock Help");
            }
        };

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.InfoMessages.ShouldContain("Mock Help");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: 500");
    }
}