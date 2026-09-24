using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/precommit.js</remarks>
public class PrecommitActionTest : EnvironmentTestBase
{
    public PrecommitActionTest()
    {
        RootCommand = new LoggingRootCommand { new PrecommitAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "precommit - successMessage"</remarks>
    [Fact]
    public void TestPrecommitSuccessMessage()
    {
        Precommit mockPrecommit = Substitute.For<Precommit>();
        mockPrecommit.Run().Returns(new PrecommitResult { SuccessMessage = "success", Warnings = [] });
        Precommit.Factory = (_, _) => mockPrecommit;

        int exitCode = RootCommand.Parse(["precommit"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("success");
    }

    /// <remarks>Refer to "precommit - success with warnings"</remarks>
    [Fact]
    public void TestPrecommitSuccessWithWarnings()
    {
        Precommit mockPrecommit = Substitute.For<Precommit>();
        mockPrecommit.Run().Returns(new PrecommitResult
        {
            SuccessMessage = "success (with 1 warning)",
            Warnings = [new CustomException(".gitignore missing", "fix: [touch .gitignore]")]
        });
        Precommit.Factory = (_, _) => mockPrecommit;

        int exitCode = RootCommand.Parse(["precommit"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("success (with 1 warning)");
        MockLogger.WarningMessages.ShouldContain(".gitignore missing. fix: [touch .gitignore]");
    }

    /// <remarks>Refer to "precommit - error raised"</remarks>
    [Fact]
    public void TestPrecommitErrorRaised()
    {
        Precommit mockPrecommit = Substitute.For<Precommit>();
        mockPrecommit.Run().Throws(new CustomException("An error occurred", "fix: [try again]"));
        Precommit.Factory = (_, _) => mockPrecommit;

        int exitCode = RootCommand.Parse(["precommit"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("An error occurred. fix: [try again]");
    }

    /// <remarks>Not from a direct JS test - confirms -i/--install parses through to Precommit.Factory.</remarks>
    [Fact]
    public void TestPrecommitInstallFlagPassedThrough()
    {
        Precommit mockPrecommit = Substitute.For<Precommit>();
        mockPrecommit.Run().Returns(new PrecommitResult { SuccessMessage = "installed", Warnings = [] });
        bool? capturedInstall = null;
        Precommit.Factory = (_, install) =>
        {
            capturedInstall = install;
            return mockPrecommit;
        };

        int exitCode = RootCommand.Parse(["precommit", "--install"]).Invoke();

        exitCode.ShouldBe(0);
        capturedInstall.ShouldBe(true);
    }
}
