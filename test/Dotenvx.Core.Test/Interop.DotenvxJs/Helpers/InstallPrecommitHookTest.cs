using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/installPrecommitHook.test.js</remarks>
public class InstallPrecommitHookTest : EnvironmentTestBase
{
    /// <remarks>Refer to "#run (exists and already includes dotenvx precommit) does nothing"</remarks>
    [Fact]
    public void TestRunExistsAndAlreadyIncludesPrecommitDoesNothing()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "dotenvx precommit";
        bool appended = false;
        Fsx.AppendFileSync = (_, _) => appended = true;

        InstallPrecommitHookResult result = new InstallPrecommitHook().Run();

        appended.ShouldBeFalse();
        result.SuccessMessage.ShouldBe($"▣ dotenvx precommit exists [{Path.Combine(".git", "hooks", "pre-commit")}]");
    }

    /// <remarks>Refer to "#run (exists and already includes dotenvx ext precommit) does nothing"</remarks>
    [Fact]
    public void TestRunExistsAndAlreadyIncludesExtPrecommitDoesNothing()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "dotenvx ext precommit";
        bool appended = false;
        Fsx.AppendFileSync = (_, _) => appended = true;

        InstallPrecommitHookResult result = new InstallPrecommitHook().Run();

        appended.ShouldBeFalse();
        result.SuccessMessage.ShouldStartWith("▣ dotenvx precommit exists");
    }

    /// <remarks>Refer to "#run (exists but does not include dotenvx precommit) appends"</remarks>
    [Fact]
    public void TestRunExistsWithoutPrecommitAppends()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "";
        bool appended = false;
        Fsx.AppendFileSync = (_, _) => appended = true;

        InstallPrecommitHookResult result = new InstallPrecommitHook().Run();

        appended.ShouldBeTrue();
        result.SuccessMessage.ShouldStartWith("▣ dotenvx precommit appended");
    }

    /// <remarks>Refer to "#run (does not exist) creates"</remarks>
    [Fact]
    public void TestRunDoesNotExistCreates()
    {
        Fsx.ExistsSync = _ => false;
        bool wrote = false;
        bool chmodded = false;
        Fsx.WriteFileX = (_, _) => wrote = true;
        Fsx.ChmodSync = (_, mode) =>
        {
            chmodded = true;
            mode.ShouldBe("755");
        };

        InstallPrecommitHookResult result = new InstallPrecommitHook().Run();

        wrote.ShouldBeTrue();
        chmodded.ShouldBeTrue();
        result.SuccessMessage.ShouldStartWith("▣ dotenvx precommit installed");
    }

    /// <remarks>Refer to "#run (fs throws an error) logs error"</remarks>
    [Fact]
    public void TestRunFsErrorWrapsAsPrecommitHookModifyFailed()
    {
        Fsx.ExistsSync = _ => false;
        Fsx.WriteFileX = (_, _) => throw new IOException("Mock Error");

        PrecommitHookModifyFailedException error = Should.Throw<PrecommitHookModifyFailedException>(() =>
            new InstallPrecommitHook().Run());

        error.MessageWithHelp.ShouldBe(
            "[PRECOMMIT_HOOK_MODIFY_FAILED] failed to modify pre-commit hook: Mock Error. fix: [try again or report error]");
    }
}
