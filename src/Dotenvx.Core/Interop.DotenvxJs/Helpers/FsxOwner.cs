using System.Runtime.InteropServices;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: write-file-atomic (used by dotenvx/src/lib/helpers/fsx.js): when `process.getuid` exists, the replacement file is
/// `chown`ed to the existing file's uid/gid, and a chown error is ignored when it is ENOSYS, or EINVAL/EPERM for a non-root caller.
/// .NET has no owner API, so this reads `st_uid`/`st_gid` through the runtime's own System.Native `SystemNative_Stat` (one fixed layout on every
/// Unix platform the runtime supports) and calls libc `chown`. Windows has no owner to copy, as in Node.
/// </remarks>
internal static class FsxOwner
{
    private const int EINVAL = 22;
    private const int ENOSYS_LINUX = 38;
    private const int ENOSYS_MAC = 78;
    private const int EPERM = 1;
    private const int UidOffset = 8;
    private const int GidOffset = 12;

    public static (uint Uid, uint Gid)? Read(string filepath)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        byte[] buffer = new byte[256];
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

        return (BitConverter.ToUInt32(buffer, UidOffset), BitConverter.ToUInt32(buffer, GidOffset));
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

    // The runtime's own System.Native shim fills a FileStatus whose layout is the same on every Unix platform the runtime supports
    // (int Flags, int Mode, uint Uid, uint Gid, ...; see pal_io.h), so no per-platform struct offsets are needed.
    [DllImport("libSystem.Native", EntryPoint = "SystemNative_Stat")]
    private static extern int Stat([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] buffer);

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int ChownNative([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint uid, uint gid);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUid();
}
