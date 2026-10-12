using System.CommandLine;
using System.Text.Json;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/get.test.js</remarks>
public class GetActionTest : EnvironmentTestBase
{
    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };
    private readonly StringWriter _consoleOutput = new() { NewLine = "\n" };
    private readonly TextWriter _originalOut = Console.Out;
    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true, NewLine = "\n" };

    public GetActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new GetAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    /// <remarks>Refer to "get - no arguments"</remarks>
    [Fact]
    public void TestGet()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse(["get"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"{JsonSerializer.Serialize(new { HELLO = "World" }, _compactJsonOptions)}\n");
    }

    /// <remarks>Refer to "get KEY"</remarks>
    [Fact]
    public void TestGetKey()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "HELLO"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("World\n");
    }

    /// <remarks>Refer to "get --format shell"</remarks>
    [Fact]
    public void TestGetFormatShell()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "shell"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO=World\n");
    }

    /// <remarks>Refer to "get - key with format json"</remarks>
    [Fact]
    public void TestGetFormatShellWithSingleQuotesInValue()
    {
        File.WriteAllText(".env", "HELLO=f'bar");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "shell"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO=f'bar\n");
    }

    /// <remarks>Refer to "get --format eval (with single quotes in value)"</remarks>
    [Fact]
    public void TestGetFormatEvalWithSingleQuotesInValue()
    {
        File.WriteAllText(".env", "HELLO=f'bar");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "eval"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO='f'\\''bar'\n");
        // Assert Get.Run() called
    }

    /// <remarks>Refer to "get --format eval (multiple keys use newlines)"</remarks>
    [Fact]
    public void TestGetFormatEvalMultipleKeysUseNewlines()
    {
        File.WriteAllText(".env", "HELLO=World\nHELLO2=World2");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "eval"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO='World'\nHELLO2='World2'\n");
    }

    /// <remarks>Refer to "get --pretty-print"</remarks>
    [Fact]
    public void TestGetPrettyPrint()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--pretty-print"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"{JsonSerializer.Serialize(new { HELLO = "World" }, _prettyPrintJsonOptions)}\n");
    }

    /// <remarks>Refer to "get KEY --convention"</remarks>
    [Fact]
    public void TestGetKeyConvention()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "HELLO", "--convention", "nextjs"]);
        int exitCode = parseResult.Invoke();

        // Real dotenvx also exits 1 here: the nextjs convention's missing .env.development.local,
        // .env.local, and .env.development files are each a non-ignored MISSING_ENV_FILE error,
        // even though HELLO is still found in the plain .env file that does exist.
        exitCode.ShouldBe(1);
        string output = _consoleOutput.ToString();
        output.ShouldBe("World\n");
    }

    /// <remarks>Refer to "get KEY (not found)"</remarks>
    [Fact]
    public void TestGetKeyNotFound()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "NOTFOUND"]);
        int exitCode = parseResult.Invoke();

        // Real dotenvx exits 1 for a non-ignored, non-strict error too.
        exitCode.ShouldBe(1);
        string output = _consoleOutput.ToString();
        output.ShouldBe("\n");
        MockLogger.ErrorMessages.ShouldContain("[MISSING_KEY] missing key (NOTFOUND). fix: [https://github.com/dotenvx/dotenvx/issues/759]");
    }

    /// <remarks>Refer to "get - key with format dotenv multiline encrypted"</remarks>
    [Fact]
    public void TestGetKeyNotFoundStrict()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "NOTFOUND", "--strict"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        string output = _consoleOutput.ToString();
        output.ShouldBe("");
        MockLogger.ErrorMessages.ShouldContain("[MISSING_KEY] missing key (NOTFOUND). fix: [https://github.com/dotenvx/dotenvx/issues/759]");
    }

    /// <remarks>Refer to "get KEY (not found) --ignore"</remarks>
    [Fact]
    public void TestGetKeyNotFoundIgnore()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "NOTFOUND", "--ignore", "MISSING_KEY"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("\n");
        MockLogger.ErrorMessages.ShouldNotContain("MISSING_KEY");
    }

    /// <remarks>
    /// Not from a JS test - regression test for a 2026-09-27 bug (see docs/porting-traps.md, trap on
    /// `OptionParsers.ParseMultiValue`). `System.CommandLine`'s `AllowMultipleArgumentsPerToken`
    /// (needed for `--include-key`'s genuine variadic syntax) greedily consumed a completely
    /// unregistered flag as just another array value instead of rejecting it, so
    /// `get --include-key HELLO --totally-bogus-flag` used to silently succeed with
    /// `includeKey = ["HELLO", "--totally-bogus-flag"]` instead of erroring the way Node's
    /// commander does for any unrecognized `--flag`-shaped token.
    /// </remarks>
    [Fact]
    public void TestIncludeKeyRejectsUnrecognizedFlagInsteadOfSwallowingIt()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse(["get", "--include-key", "HELLO", "--totally-bogus-flag"]);

        parseResult.Errors.ShouldNotBeEmpty();
        int exitCode = parseResult.Invoke();
        exitCode.ShouldNotBe(0);
    }
}