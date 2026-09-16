using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/decrypt.test.js</remarks>
public class DecryptActionTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;

    public DecryptActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new DecryptAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    /// <remarks>Refer to "decrypt - nothing"</remarks>
    [Fact]
    public void TestDecryptNothing()
    {
        File.WriteAllText(".env", "");
        ParseResult parseResult = RootCommand.Parse(["decrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"decrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldNotContain($"decrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldNotContain("✔ decrypted (.env)");
        MockLogger.InfoMessages.ShouldContain($"no changes .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("no changes (.env)");
    }

    /// <remarks>Refer to "dec" shorthand alias for "decrypt" (v2.21.0)</remarks>
    [Fact]
    public void TestDecAliasRunsDecrypt()
    {
        File.WriteAllText(".env", "");
        ParseResult parseResult = RootCommand.Parse(["dec"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"decrypting .env ({Path.GetFullPath(".env")})");
    }

    /// <remarks>Refer to "decrypt - .env but no changes"</remarks>
    [Fact]
    public void TestDecryptEnvButNoChanges()
    {
        File.WriteAllText(".env", "HELLO=\"World\"");

        ParseResult parseResult = RootCommand.Parse(["decrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain($"no changes .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("no changes (.env)");
    }

    /// <remarks>Refer to "decrypt - --stdout"</remarks>
    [Fact]
    public void TestDecryptStdout()
    {
        File.WriteAllText(".env", "HELLO=\"World\"");

        ParseResult parseResult = RootCommand.Parse(["decrypt", "--stdout"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO=\"World\"\n");
    }

    /// <remarks>Refer to "decrypt - --stdout with error"</remarks>
    [Fact]
    public void TestDecryptStdoutWithError()
    {
        // Simulate an error within the Decrypt.Run
        using FileStream fs = new(".env", FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(""u8);

        ParseResult parseResult = RootCommand.Parse(["decrypt", "--stdout"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(s => s.StartsWith("The process cannot access the file"));
    }

    /// <remarks>Refer to "decrypt - .env with changes"</remarks>
    [Fact]
    public void TestDecryptEnvWithChanges()
    {
        File.Copy("tests/monorepo/apps/encrypted/.env.keys", ".env.keys");
        File.Copy("tests/monorepo/apps/encrypted/.env", ".env");

        ParseResult parseResult = RootCommand.Parse(["decrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.VerboseMessages.ShouldContain($"decrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.VerboseMessages.ShouldContain($"decrypted .env ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain("✔ decrypted (.env)");
    }

    /// <remarks>Refer to "decrypt - MISSING_ENV_FILE"</remarks>
    [Fact]
    public void TestDecryptMissingEnvFile()
    {
        ParseResult parseResult = RootCommand.Parse(["decrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        MockLogger.ErrorMessages.ShouldContain($"[MISSING_ENV_FILE] missing .env file ({Path.GetFullPath(".env")})");
        MockLogger.InfoMessages.ShouldContain(
            "? add one with [echo \"HELLO=World\" > .env] and re-run [Dotenvx decrypt]");
    }

    /// <remarks>Refer to "decrypt - OTHER_ERROR"</remarks>
    [Fact]
    public void TestDecryptOtherError()
    {
        // Simulate an error within the Decrypt.Run
        using FileStream fs = new(".env", FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(""u8);

        ParseResult parseResult = RootCommand.Parse(["decrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        MockLogger.VerboseMessages.ShouldContain($"decrypting .env ({Path.GetFullPath(".env")})");
        MockLogger.ErrorMessages.ShouldContain(s => s.StartsWith("The process cannot access the file"));
    }

    /// <remarks>Refer to "decrypt - catch error"</remarks>
    [Fact]
    public void TestDecryptCatchError()
    {
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        MockLogger.BeforeLog = message =>
        {
            if (message.StartsWith("decrypting"))
            {
                throw new DotenvxException("500", "Mock Error", "Mock Help");
            }
        };

        ParseResult parseResult = RootCommand.Parse(["decrypt"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("Mock Error");
        MockLogger.InfoMessages.ShouldContain("Mock Help");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: 500");
    }
}