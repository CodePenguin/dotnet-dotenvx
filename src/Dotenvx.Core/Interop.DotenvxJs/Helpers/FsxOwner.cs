using System.Runtime.InteropServices;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: write-file-atomic (used by dotenvx/src/lib/helpers/fsx.js): when `process.getuid` exists, the replacement file is
/// `chown`ed to the existing file's uid/gid, and a chown error is ignored when it is ENOSYS, or EINVAL/EPERM for a non-root caller.
/// .NET has no owner API, so this reads `st_uid`/`st_gid` through libc `stat` (the struct offsets of Linux x64/arm64 and macOS) and
/// calls libc `chown`. A platform whose layout is unknown, or without a `stat` symbol (glibc before 2.33), reports no owner, so the
/// chown is skipped there.
/// </remarks>
internal static class FsxOwner
{
    private const int EINVAL = 22;
    private const int ENOSYS_LINUX = 38;
    private const int ENOSYS_MAC = 78;
    private const int EPERM = 1;

    public static (uint Uid, uint Gid)? Read(string filepath)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        (int uidOffset, int gidOffset)? layout = Layout();
        if (layout is null)
        {
            return null;
        }

        byte[] buffer = new byte[512];
        try
        {
            if (Stat(filepath, buffer) != 0)
            {
                return null;
            }
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }

        return (BitConverter.ToUInt32(buffer, layout.Value.uidOffset), BitConverter.ToUInt32(buffer, layout.Value.gidOffset));
    }

    // write-file-atomic's `fs.chownSync(tmpfile, uid, gid)` with its isChownErrOk filter
    public static void Chown(string filepath, uint uid, uint gid)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        int result;
        int errno;
        try
        {
            result = ChownNative(filepath, uid, gid);
            errno = Marshal.GetLastPInvokeError();
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return;
        }

        if (result == 0)
        {
            return;
        }

        bool nonRoot = GetUid() != 0;
        if (errno is ENOSYS_LINUX or ENOSYS_MAC || (nonRoot && errno is EINVAL or EPERM))
        {
            return;
        }

        throw new IOException($"chown failed (errno {errno}): {filepath}");
    }

    private static (int, int)? Layout()
    {
        if (OperatingSystem.IsMacOS())
        {
            return (16, 20);
        }

        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => (28, 32),
                Architecture.Arm64 => (24, 28),
                _ => null
            };
        }

        return null;
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] buffer);

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int ChownNative([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint uid, uint gid);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUid();
}
