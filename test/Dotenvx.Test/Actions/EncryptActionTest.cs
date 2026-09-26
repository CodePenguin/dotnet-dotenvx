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

    /// <remarks>
    /// Refer to "encrypt - nothing". Verified against the real Node CLI: an empty .env file is
    /// NOT a no-op - upstream's encryptTransform treats it the same as a missing file, replacing
    /// it with the sample kit (HELLO="World") and encrypting that, per
    /// dotenvx/src/lib/transforms/encrypt.js's `if (row.envSrc.trim().length === 0)` branch.
    /// </remarks>
    [Fact]
    public void TestEncryptNothing()
    {
        File.WriteAllText(".env", "");
        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"encrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("◈ encrypted (.env)");
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
        MockLogger.InfoMessages.ShouldContain("○ no change (.env)");
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
        MockLogger.InfoMessages.ShouldContain("◈ encrypted (.env)");
    }

    /// <remarks>
    /// No longer a distinct current upstream scenario - historical coverage from a
    /// "privateKeyAdded" hint-printing feature that doesn't exist in current encrypt.js (removed
    /// 2026-09-25, see EncryptAction.cs's own class remarks). Kept because the underlying scenario
    /// (encrypting a fresh .env with no pre-existing .env.keys, so a brand-new key gets generated)
    /// is still valid coverage, distinct from TestEncryptEnvWithChanges's reused-existing-key path.
    /// </remarks>
    [Fact]
    public void TestEncryptEnvWithChangesGeneratesFreshKeyWhenNoneExists()
    {
        File.WriteAllText(".env", "HELLO=World");

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"encrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("◈ encrypted (.env)");
    }

    /// <remarks>
    /// Verified against the real Node CLI: a missing .env file is NOT an error by default -
    /// encrypt creates a sample file (HELLO="World") and encrypts it, per
    /// dotenvx/src/lib/transforms/encrypt.js's `if (!fileExists &amp;&amp; !noCreate)` branch.
    /// The MISSING_ENV_FILE error scenario below (<see cref="TestEncryptMissingEnvFileWithNoCreate"/>)
    /// only happens with --no-create.
    /// </remarks>
    [Fact]
    public void TestEncryptMissingEnvFile()
    {
        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"encrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("◈ encrypted (.env)");
        File.ReadAllText(".env").ShouldContain("encrypted:");
    }

    /// <remarks>Refer to "encrypt - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestEncryptMissingEnvFileWithNoCreate()
    {
        ParseResult parseResult = RootCommand.Parse(["encrypt", "--no-create"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_ENV_FILE] missing file (.env). fix: [https://github.com/dotenvx/dotenvx/issues/484]");
    }

    /// <remarks>
    /// Refer to "encrypt - OTHER_ERROR". Verified against the real Node CLI (EncryptAction
    /// previously never returned a non-zero exit code for a per-file processing error - only the
    /// outer catch block did - confirmed as a real, independent bug via a live comparison, not
    /// just from this test: `node dotenvx.js encrypt` against an unreadable .env exits 1).
    /// </remarks>
    [Fact]
    public void TestEncryptOtherError()
    {
        // Simulate an error within the Decrypt.Run
        using FileStream fs = new(".env", FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(""u8);

        ParseResult parseResult = RootCommand.Parse(["encrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.VerboseMessages.ShouldContain($"encrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(s => s.StartsWith("The process cannot access the file"));
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
        MockLogger.ErrorMessages.ShouldContain("Mock Error. Mock Help");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: 500");
    }
}