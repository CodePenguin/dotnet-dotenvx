using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Logging;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/check.js (hidden `check`). Not from a JS test (upstream covers it through the e2e
/// suite); the expected lines were produced by the real Node CLI from the same files (see CoreCommandsParityTest and
/// ProjectCommandsParityTest for the byte comparison).
/// </remarks>
[Collection("Fsx")]
public class CheckActionTest : EnvironmentTestBase
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-check-" + Guid.NewGuid().ToString("N"));

    public CheckActionTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
        RootCommand = new LoggingRootCommand { new CheckAction(MockLogger, new DotenvxLoggerOptions()) };
    }

    private RootCommand RootCommand { get; }

    ~CheckActionTest()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private int Check(params string[] args) => RootCommand.Parse(["check", "--no-armor", "--no-native", .. args]).Invoke();

    [Fact]
    public void CheckNeedsAnEnvfile()
    {
        Check().ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(message => message.Contains("ENVFILE_REQUIRED") && message.Contains("check requires an Envfile"));
    }

    [Fact]
    public void CheckPassesWhenTheEnvFileSatisfiesTheEnvfile()
    {
        File.WriteAllText("Envfile", "strict true\n\nenv \"A\"\nenv \"B\"\n");
        File.WriteAllText(".env", "A=1\nB=2\n");

        Check("-f", ".env").ShouldBe(0);

        MockLogger.InfoMessages.Count.ShouldBe(2);
        MockLogger.InfoMessages[0].ShouldContain("A=[REDACTED]");
        MockLogger.InfoMessages[0].ShouldContain("# .env:1");
        MockLogger.InfoMessages[1].ShouldContain("B=[REDACTED]");
        MockLogger.ErrorMessages.ShouldBeEmpty();
    }

    [Fact]
    public void CheckFailsAndPreviewsMissingAndUndeclaredKeys()
    {
        File.WriteAllText("Envfile", "strict true\n\nenv \"A\"\nenv \"B\"\n");
        File.WriteAllText(".env", "A=1\nC=3\n");

        Check("-f", ".env").ShouldBe(1);

        MockLogger.InfoMessages.ShouldContain(line => line.Contains("C=[REDACTED]") && line.Contains("# .env:2 !declared"));
        MockLogger.InfoMessages.ShouldContain(line => line.Contains("B=[MISSING]") && line.Contains("# !required"));
    }

    [Fact]
    public void CheckReportsAMissingEnvFileAndThatNothingWasChecked()
    {
        File.WriteAllText("Envfile", "strict true\n\nenv \"A\"\n");

        Check("-f", "missing.env").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldContain(message => message.Contains("MISSING_ENV_FILE") && message.Contains("missing.env"));
        MockLogger.ErrorMessages.ShouldContain("no environment sources found to check");
    }

    [Fact]
    public void CheckIgnoresErrorCodesItIsToldTo()
    {
        File.WriteAllText("Envfile", "strict true\n\nenv \"A\"\n");

        Check("-f", "missing.env", "--ignore", "MISSING_ENV_FILE", "--ignore", "MISSING_REQUIRED_KEY").ShouldBe(1);

        MockLogger.ErrorMessages.ShouldNotContain(message => message.Contains("MISSING_ENV_FILE"));
        MockLogger.ErrorMessages.ShouldContain("no environment sources found to check");
    }
}
