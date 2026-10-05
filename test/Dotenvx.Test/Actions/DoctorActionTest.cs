using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/doctor.js</remarks>
public class DoctorActionTest : EnvironmentTestBase
{
    public DoctorActionTest()
    {
        RootCommand = new LoggingRootCommand { new DoctorAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "doctor calls Doctor.run and prints findings"</remarks>
    [Fact]
    public void TestDoctorCallsRunAndPrintsFindings()
    {
        Doctor mockDoctor = Substitute.For<Doctor>();
        mockDoctor.Run().Returns([
            new DoctorFinding("Python", "app.py", 2, "load_dotenv()", "found python-dotenv load call")
        ]);
        Doctor.Factory = _ => mockDoctor;

        int exitCode = RootCommand.Parse(["doctor", "."]).Invoke();

        exitCode.ShouldBe(0);
        mockDoctor.Received(1).Run();
        MockLogger.WarningMessages.ShouldContain("found 1 possible dotenv loader");
        MockLogger.InfoMessages.ShouldContain("│ app.py:2: load_dotenv()");
    }

    /// <remarks>Refer to "doctor pluralizes loader count"</remarks>
    [Fact]
    public void TestDoctorPluralizesLoaderCount()
    {
        Doctor mockDoctor = Substitute.For<Doctor>();
        mockDoctor.Run().Returns([
            new DoctorFinding("Python", "app.py", 2, "load_dotenv()", "found python-dotenv load call"),
            new DoctorFinding("Node", "index.js", 1, "require('dotenv').config()", "found dotenv config require call")
        ]);
        Doctor.Factory = _ => mockDoctor;

        RootCommand.Parse(["doctor", "."]).Invoke();

        MockLogger.WarningMessages.ShouldContain("found 2 possible dotenv loaders");
    }

    /// <remarks>Refer to "doctor prints clean message when no findings are found"</remarks>
    [Fact]
    public void TestDoctorPrintsCleanMessageWhenNoFindingsAreFound()
    {
        Doctor mockDoctor = Substitute.For<Doctor>();
        mockDoctor.Run().Returns([]);
        Doctor.Factory = _ => mockDoctor;

        RootCommand.Parse(["doctor", "."]).Invoke();

        MockLogger.InfoMessages.ShouldContain("no dotenv loaders found");
    }
}
