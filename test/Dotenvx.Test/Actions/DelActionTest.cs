using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Moq;

using Shouldly;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/del.js. Behaviors verified directly against the real
/// dotenvx v2.24.1 Node CLI, since del isn't (yet) covered by a tests/cli/actions/del.test.js file
/// upstream.
/// </remarks>
public class DelActionTest : EnvironmentTestBase
{
    public DelActionTest()
    {
        RootCommand = new LoggingRootCommand { new DelAction(MockLogger) };
    }

    private RootCommand RootCommand { get; }

    /// <remarks>Refer to "del - no change"</remarks>
    [Fact]
    public void TestDelNoChange()
    {
        Mock<Del> mockDel = new();
        mockDel.Setup(d => d.Run()).Returns(new DelResult
        {
            ProcessedEnvs =
            [
                new ProcessedDel
                {
                    Key = "NOTFOUND", Type = EnvType.EnvFile, FilePath = ".env", EnvFilePath = ".env",
                    EnvSrc = "HELLO=World", Changed = false
                }
            ],
            ChangedFilePaths = [],
            UnchangedFilePaths = [".env"]
        });
        Del.Factory = (_, _) => mockDel.Object;

        ParseResult parseResult = RootCommand.Parse(["del", "NOTFOUND"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockDel.Verify(d => d.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("○ no change (.env)");
    }

    /// <remarks>Refer to "del - changes"</remarks>
    [Fact]
    public void TestDelChanges()
    {
        Fsx.WriteFileX = (_, _) => { };
        Mock<Del> mockDel = new();
        mockDel.Setup(d => d.Run()).Returns(new DelResult
        {
            ProcessedEnvs =
            [
                new ProcessedDel
                {
                    Key = "HELLO", Type = EnvType.EnvFile, FilePath = ".env", EnvFilePath = ".env",
                    EnvSrc = "FOO=bar", Changed = true
                }
            ],
            ChangedFilePaths = [".env"],
            UnchangedFilePaths = []
        });
        Del.Factory = (_, _) => mockDel.Object;

        ParseResult parseResult = RootCommand.Parse(["del", "HELLO"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);
        mockDel.Verify(d => d.Run(), Times.Once);
        MockLogger.InfoMessages.ShouldContain("◇ removed HELLO (.env)");
    }

    /// <remarks>Refer to "del - missing env file"</remarks>
    [Fact]
    public void TestDelMissingEnvFile()
    {
        Mock<Del> mockDel = new();
        mockDel.Setup(d => d.Run()).Returns(new DelResult
        {
            ProcessedEnvs =
            [
                new ProcessedDel
                {
                    Key = "HELLO", Type = EnvType.EnvFile, FilePath = ".env", EnvFilePath = ".env",
                    Error = new MissingEnvFileException(".env", ".env")
                }
            ],
            ChangedFilePaths = [],
            UnchangedFilePaths = []
        });
        Del.Factory = (_, _) => mockDel.Object;

        ParseResult parseResult = RootCommand.Parse(["del", "HELLO"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);
        MockLogger.ErrorMessages.ShouldContain(m => m.Contains("MISSING_ENV_FILE"));
    }
}
