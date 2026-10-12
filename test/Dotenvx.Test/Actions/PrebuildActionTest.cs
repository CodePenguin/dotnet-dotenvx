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

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/prebuild.js</remarks>
public class PrebuildActionTest : EnvironmentTestBase
{
    public PrebuildActionTest()
    {
        RootCommand = new LoggingRootCommand { new PrebuildAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "prebuild - successMessage"</remarks>
    [Fact]
    public void TestPrebuildSuccessMessage()
    {
        Prebuild mockPrebuild = Substitute.For<Prebuild>();
        mockPrebuild.Run().Returns(new PrebuildResult { SuccessMessage = "success", Warnings = [] });
        Prebuild.Factory = _ => mockPrebuild;

        int exitCode = RootCommand.Parse(["prebuild"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("success");
    }

    /// <remarks>Refer to "prebuild - success with warnings"</remarks>
    [Fact]
    public void TestPrebuildSuccessWithWarnings()
    {
        Prebuild mockPrebuild = Substitute.For<Prebuild>();
        mockPrebuild.Run().Returns(new PrebuildResult
        {
            SuccessMessage = "success (with 1 warning)",
            Warnings = [new CustomException(".dockerignore missing", "fix: [touch .dockerignore]")]
        });
        Prebuild.Factory = _ => mockPrebuild;

        int exitCode = RootCommand.Parse(["prebuild"]).Invoke();

        exitCode.ShouldBe(0);
        MockLogger.InfoMessages.ShouldContain("success (with 1 warning)");
        MockLogger.WarningMessages.ShouldContain(".dockerignore missing. fix: [touch .dockerignore]");
    }

    /// <remarks>Refer to "prebuild - error raised"</remarks>
    [Fact]
    public void TestPrebuildErrorRaised()
    {
        Prebuild mockPrebuild = Substitute.For<Prebuild>();
        mockPrebuild.Run().Throws(new CustomException("An error occurred", "fix: [try again]"));
        Prebuild.Factory = _ => mockPrebuild;

        int exitCode = RootCommand.Parse(["prebuild"]).Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain("An error occurred. fix: [try again]");
    }
}
