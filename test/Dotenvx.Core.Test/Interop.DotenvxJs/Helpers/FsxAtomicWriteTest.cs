using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/fsx.test.js (the atomic `writeFileXSync` and `init` overwrite tests added in
/// 2.35.0). Upstream stubs `fs.writeSync`/`fs.renameSync`/`fs.realpathSync` with sinon; here the same failure points are the
/// `Fsx.StageWrite` (write into the temporary file) and `Fsx.StageMove` (rename over the destination) seams.
/// </remarks>
[Collection("Fsx")]
public class FsxAtomicWriteTest : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-fsx-" + Guid.NewGuid().ToString("N"));

    public FsxAtomicWriteTest()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Fsx.ResetToDefaults();
        Directory.Delete(_dir, true);
    }

    private string P(string name) => Path.Combine(_dir, name);

    private static IOException NoSpace() => new("ENOSPC: no space left on device, write");

    private string[] Staging() => Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName)
        .Where(name => name!.StartsWith(".dotenvx-write-")).ToArray()!;

    /// <remarks>Refer to "#writeFileXSync replaces existing contents on success"</remarks>
    [Fact]
    public void ReplacesExistingContentsOnSuccess()
    {
        File.WriteAllText(P("attributes"), "old contents\n");

        Fsx.WriteFileX(P("attributes"), "new contents\n");

        File.ReadAllText(P("attributes")).ShouldBe("new contents\n");
    }

    /// <remarks>Refer to "#writeFileXSync leaves original contents intact when the write fails with ENOSPC"</remarks>
    [Fact]
    public void LeavesOriginalContentsIntactWhenTheWriteFails()
    {
        File.WriteAllText(P("attributes"), "original contents\n");
        Fsx.StageWrite = (_, _) => throw NoSpace();

        Should.Throw<IOException>(() => Fsx.WriteFileX(P("attributes"), "replacement contents\n"));

        File.ReadAllText(P("attributes")).ShouldBe("original contents\n");
    }

    /// <remarks>Refer to "#init leaves an existing Envfile intact when the atomic write fails with ENOSPC"</remarks>
    [Fact]
    public void InitLeavesAnExistingEnvfileIntactWhenTheAtomicWriteFails()
    {
        File.WriteAllText(P("Envfile"), "ORIGINAL\n");
        File.WriteAllText(P(".env.example"), "HELLO=World\n");
        Fsx.StageWrite = (_, _) => throw NoSpace();

        Should.Throw<IOException>(() => EnvfileInit.Init(_dir, ".env.example", overwrite: true));

        File.ReadAllText(P("Envfile")).ShouldBe("ORIGINAL\n");
    }

    /// <remarks>Refer to "#init respects exclusive creation without overwrite"</remarks>
    [Fact]
    public void InitRespectsExclusiveCreationWithoutOverwrite()
    {
        File.WriteAllText(P("Envfile"), "ORIGINAL\n");
        File.WriteAllText(P(".env.example"), "HELLO=World\n");

        EnvfileInit.Init(_dir, ".env.example").Created.ShouldBeFalse();

        File.ReadAllText(P("Envfile")).ShouldBe("ORIGINAL\n");
    }

    /// <remarks>Refer to "#init creates an Envfile when none exists"</remarks>
    [Fact]
    public void InitCreatesAnEnvfileWhenNoneExists()
    {
        File.WriteAllText(P(".env.example"), "HELLO=World\n");

        EnvfileInit.Init(_dir, ".env.example").Created.ShouldBeTrue();

        File.Exists(P("Envfile")).ShouldBeTrue();
    }

    /// <remarks>Refer to "#init overwrite rejects existing symlinks"</remarks>
    [Fact]
    public void InitOverwriteRejectsExistingSymlinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.WriteAllText(P("other"), "KEEP\n");
        File.CreateSymbolicLink(P("Envfile"), P("other"));

        Should.Throw<Exception>(() => EnvfileInit.Init(_dir, overwrite: true)).Message.ShouldContain("expected a regular file");

        File.ReadAllText(P("other")).ShouldBe("KEEP\n");
    }

    /// <remarks>Refer to "#init overwrite never follows a symlink introduced during the atomic write"</remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InitOverwriteNeverFollowsASymlinkIntroducedDuringTheAtomicWrite(bool initiallyExists)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (initiallyExists)
        {
            File.WriteAllText(P("Envfile"), "ORIGINAL\n");
        }

        File.WriteAllText(P("other"), "KEEP\n");
        File.WriteAllText(P(".env"), "HELLO=World\n");
        bool swapped = false;
        Action<string, string> write = Fsx.StageWrite;
        Fsx.StageWrite = (path, data) =>
        {
            if (!swapped)
            {
                swapped = true;
                File.Delete(P("Envfile"));
                File.CreateSymbolicLink(P("Envfile"), P("other"));
            }

            write(path, data);
        };

        Should.Throw<Exception>(() => EnvfileInit.Init(_dir, overwrite: true)).Message.ShouldContain("expected a regular file");

        swapped.ShouldBeTrue("swaps the destination while the atomic write runs");
        File.ReadAllText(P("other")).ShouldBe("KEEP\n");
        Staging().ShouldBeEmpty();
    }

    /// <remarks>Refer to "#init overwrite cannot follow a symlink swapped immediately before the final rename"</remarks>
    [Fact]
    public void InitOverwriteCannotFollowASymlinkSwappedImmediatelyBeforeTheFinalRename()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.WriteAllText(P("Envfile"), "ORIGINAL\n");
        File.WriteAllText(P("other"), "KEEP\n");
        File.WriteAllText(P(".env"), "HELLO=World\n");
        bool swapped = false;
        Action<string, string> move = Fsx.StageMove;
        Fsx.StageMove = (from, to) =>
        {
            if (to == P("Envfile"))
            {
                swapped = true;
                File.Delete(P("Envfile"));
                File.CreateSymbolicLink(P("Envfile"), P("other"));
            }

            move(from, to);
        };

        EnvfileInit.Init(_dir, overwrite: true).Created.ShouldBeTrue();

        swapped.ShouldBeTrue();
        File.ReadAllText(P("other")).ShouldBe("KEEP\n");
        new FileInfo(P("Envfile")).LinkTarget.ShouldBeNull("rename replaces the link itself");
        File.ReadAllText(P("Envfile")).ShouldContain("HELLO");
    }

    /// <remarks>Refer to "#init atomic overwrite preserves permissions and cleans staging after failure"</remarks>
    [Fact]
    public void InitAtomicOverwritePreservesPermissionsAndCleansStagingAfterFailure()
    {
        File.WriteAllText(P("Envfile"), "ORIGINAL\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(P("Envfile"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.WriteAllText(P(".env"), "HELLO=World\n");

        EnvfileInit.Init(_dir, overwrite: true).Created.ShouldBeTrue();

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(P("Envfile")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        string source = File.ReadAllText(P("Envfile"));
        Fsx.StageWrite = (_, _) => throw NoSpace();

        Should.Throw<IOException>(() => EnvfileInit.Init(_dir, overwrite: true));

        File.ReadAllText(P("Envfile")).ShouldBe(source);
        Staging().ShouldBeEmpty();
    }

    /// <remarks>Refer to "#init overwrite creates a missing Envfile atomically"</remarks>
    [Fact]
    public void InitOverwriteCreatesAMissingEnvfileAtomically()
    {
        File.WriteAllText(P(".env"), "HELLO=World\n");

        EnvfileInit.Init(_dir, overwrite: true).Created.ShouldBeTrue();

        File.ReadAllText(P("Envfile")).ShouldContain("HELLO");
        Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe([".env", "Envfile"]);
    }

    /// <remarks>Refer to "#init preserves the original when the final rename fails"</remarks>
    [Fact]
    public void InitPreservesTheOriginalWhenTheFinalRenameFails()
    {
        File.WriteAllText(P("Envfile"), "ORIGINAL\n");
        Action<string, string> move = Fsx.StageMove;
        Fsx.StageMove = (from, to) =>
        {
            if (to == P("Envfile"))
            {
                throw NoSpace();
            }

            move(from, to);
        };

        Should.Throw<IOException>(() => EnvfileInit.Init(_dir, overwrite: true));

        File.ReadAllText(P("Envfile")).ShouldBe("ORIGINAL\n");
        Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName).ShouldBe(["Envfile"]);
    }

    /// <remarks>Refer to "#writeFileXSync still follows symlinks by default"</remarks>
    [Fact]
    public void StillFollowsSymlinksByDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.WriteAllText(P("target"), "ORIGINAL\n");
        File.CreateSymbolicLink(P("link"), P("target"));

        Fsx.WriteFileX(P("link"), "UPDATED\n");

        new FileInfo(P("link")).LinkTarget.ShouldNotBeNull();
        File.ReadAllText(P("target")).ShouldBe("UPDATED\n");
    }
}
