using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/tests/cli/actions/spinner-coverage.test.js</remarks>
public class SpinnerActionsTest : EnvironmentTestBase
{
    private sealed class SpinnerSpy : ISpinner
    {
        public int Stops { get; private set; }
        public string Text { get; set; } = "";
        public void Start() { }
        public void Stop() => Stops++;
    }

    private readonly SpinnerSpy _spinner = new();

    public SpinnerActionsTest()
    {
        Spinners.CreateSpinner = _ => _spinner;
    }

    private RootCommand Root()
    {
        return new LoggingRootCommand { new KeyPairAction(MockLogger), new RunAction(MockLogger) };
    }

    /// <remarks>Refer to "keypair stops spinner before output"</remarks>
    [Fact]
    public void TestKeypairStopsSpinnerBeforeOutput()
    {
        File.WriteAllText(".env", "DOTENV_PUBLIC_KEY=\"<publicKey>\"\n");
        var output = new StringWriter { NewLine = "\n" };
        TextWriter original = Console.Out;
        Console.SetOut(output);
        int stopsAtOutput = -1;
        try
        {
            Root().Parse(["keypair", "DOTENV_PUBLIC_KEY", "--no-armor"]).Invoke().ShouldBe(0);
            stopsAtOutput = _spinner.Stops;
        }
        finally
        {
            Console.SetOut(original);
        }

        _spinner.Stops.ShouldBe(1, "spinner.stop called before output");
        stopsAtOutput.ShouldBe(1);
        output.ToString().ShouldContain("<publicKey>");
    }

    /// <remarks>Refer to "run stops spinner when command missing"</remarks>
    [Fact]
    public void TestRunStopsSpinnerWhenCommandMissing()
    {
        Root().Parse(["run", "--no-armor", "--no-native"]).Invoke().ShouldBe(1);

        _spinner.Stops.ShouldBe(1, "spinner.stop called in missing-command branch");
    }

    /// <remarks>Refer to "run stops spinner on success path"</remarks>
    [Fact]
    public void TestRunStopsSpinnerOnSuccessPath()
    {
        Root().Parse(["run", "--no-armor", "--no-native", "--", "dotnet", "--version"]).Invoke();

        _spinner.Stops.ShouldBe(1, "spinner.stop called on success branch");
    }

    /// <remarks>Refer to "run stops spinner on catch path"</remarks>
    [Fact]
    public void TestRunStopsSpinnerOnCatchPath()
    {
        Run.Factory = (_, _, _, _) => throw new InvalidOperationException("boom");

        Root().Parse(["run", "--no-armor", "--no-native", "--", "echo", "ok"]).Invoke().ShouldBe(1);

        _spinner.Stops.ShouldBe(1, "spinner.stop called in catch branch");
        MockLogger.ErrorMessages.ShouldContain("boom");
    }
}
