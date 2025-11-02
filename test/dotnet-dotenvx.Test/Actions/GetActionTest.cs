using System.CommandLine;
using System.Text.Json;

using dotnet_dotenvx.Actions;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

public class GetActionTest : EnvironmentTestBase
{
    private readonly StringWriter _consoleOutput = new();
    private readonly TextWriter _originalOut = Console.Out;
    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };
    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true };

    public GetActionTest()
    {
        Console.SetOut(_consoleOutput);
        _consoleOutput.GetStringBuilder().Clear();
        RootCommand = new LoggingRootCommand { new GetAction(MockLogger) };
    }

    private MockLogger MockLogger { get; } = new();
    private RootCommand RootCommand { get; }

    protected override void TearDown()
    {
        base.TearDown();
        Console.SetOut(_originalOut);
    }

    [Fact]
    public void Get()
    {
        File.WriteAllText(".env", "HELLO=World");
        ParseResult parseResult = RootCommand.Parse(["get"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"{JsonSerializer.Serialize(new { HELLO = "World" }, _compactJsonOptions)}\n");
    }

    [Fact]
    public void GetKey()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "HELLO"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("World\n");
    }

    [Fact]
    public void GetFormatShell()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "shell"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO=World\n");
    }

    [Fact]
    public void GetFormatShellWithSingleQuotesInValue()
    {
        File.WriteAllText(".env", "HELLO=f'bar");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "shell"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("HELLO=f'bar\n");
    }

    [Fact]
    public void GetFormatEvalWithSingleQuotesInValue()
    {
        File.WriteAllText(".env", "HELLO=f'bar");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "eval"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"HELLO={JsonSerializer.Serialize("f'bar")}\n");
        // Assert Get.Run() called
    }

    [Fact]
    public void GetFormatEvalMultipleKeysUseNewlines()
    {
        File.WriteAllText(".env", "HELLO=World\nHELLO2=World2");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--format", "eval"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"HELLO={JsonSerializer.Serialize("World")}\nHELLO2={JsonSerializer.Serialize("World2")}\n");
    }

    [Fact]
    public void GetPrettyPrint()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "--pretty-print"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe($"{JsonSerializer.Serialize(new { HELLO = "World" }, _prettyPrintJsonOptions)}\n");
    }

    [Fact]
    public void GetKeyConvention()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "HELLO", "--convention", "nextjs"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("World\n");
    }

    [Fact]
    public void GetKeyNotFound()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "NOTFOUND"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("\n");
        MockLogger.ErrorMessages.ShouldContain("[MISSING_KEY] missing NOTFOUND key");
    }

    [Fact]
    public void GetKeyNotFoundStrict()
    {
        File.WriteAllText(".env", "HELLO=World");
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "NOTFOUND", "--strict"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        string output = _consoleOutput.ToString();
        output.ShouldBe("");
        MockLogger.ErrorMessages.ShouldContain("[MISSING_KEY] missing NOTFOUND key");
    }

    [Fact]
    public void GetKeyNotFoundIgnore()
    {
        File.WriteAllText(".env", "HELLO=World");
        // Mock Get.Run() needed to return { Parsed = new Dictionary<string, string> { ["HELLO"] = "World" }, Errors = new List<Exception> { error } } where error.Message = "MISSING_KEY", error is DotenvxException with Code = "MISSING_KEY", Help = "some help"
        _consoleOutput.GetStringBuilder().Clear();
        ParseResult parseResult = RootCommand.Parse(["get", "NOTFOUND", "--ignore", "MISSING_KEY"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        string output = _consoleOutput.ToString();
        output.ShouldBe("\n");
        MockLogger.ErrorMessages.ShouldNotContain("MISSING_KEY");
    }
}