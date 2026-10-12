using System.CommandLine;

using Dotenvx;
using Dotenvx.Actions;
using Dotenvx.Core.Test;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/update.js</remarks>
public class UpdateActionTest : EnvironmentTestBase
{
    /// <remarks>
    /// Refer to "update prints manual npm commands when run through node". The other upstream scenario ("update runs
    /// the installer when packaged as a binary", `process.pkg`) cannot exist in this port: it is never a pkg binary.
    /// </remarks>
    [Fact]
    public void TestUpdatePrintsManualNpmCommands()
    {
        var output = new StringWriter { NewLine = "\n" };
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            int exitCode = new LoggingRootCommand { new UpdateAction() }.Parse(["update"]).Invoke();

            exitCode.ShouldBe(0);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().ShouldBe(
            "global: npm install -g @dotenvx/dotenvx@latest\nlocal:  npm install @dotenvx/dotenvx@latest --save\n");
    }
}
