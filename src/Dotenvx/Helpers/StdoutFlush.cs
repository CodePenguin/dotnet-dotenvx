using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Dotenvx.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/cli/commandAction.js. Once an action returns an exit code, dotenvx flushes stdout with
/// `stream.write('')` before exiting. When the reader of stdout has gone away and dotenvx never wrote to stdout itself
/// (a write that failed would already have destroyed the stream, which the flush skips), that empty write fails with EPIPE
/// as an uncaught error and the process exits 1. .NET's console streams swallow broken pipes, so the empty write is made
/// directly on the stdout handle.
/// </remarks>
internal static class StdoutFlush
{
    private const int StdOutputHandle = -11;
    private const int ErrorBrokenPipe = 109;
    private const int ErrorNoData = 232;
    private const int Epipe = 32;

    private static bool _written;

    /// <summary>Wraps Console.Out so the flush can tell whether dotenvx wrote to stdout.</summary>
    public static void Track()
    {
        TextWriter inner = Console.Out;
        Console.SetOut(new TrackingWriter(inner));
    }

    /// <summary>Returns 1 (after reporting on stderr) when the empty flush write fails because the reader is gone.</summary>
    public static int Finish(int exitCode)
    {
        if (_written || !IsBroken())
        {
            return exitCode;
        }

        Console.Error.WriteLine("Unhandled exception: System.IO.IOException: write EPIPE");

        return 1;
    }

    private static bool IsBroken()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                IntPtr handle = GetStdHandle(StdOutputHandle);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                {
                    return false;
                }

                if (WriteFile(handle, [], 0, out _, IntPtr.Zero))
                {
                    return false;
                }

                int error = Marshal.GetLastWin32Error();

                return error is ErrorBrokenPipe or ErrorNoData;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return Write(1, IntPtr.Zero, UIntPtr.Zero) < 0 && Marshal.GetLastWin32Error() == Epipe;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
        {
            // no native entry point: leave the exit code alone
        }

        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int stdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(IntPtr handle, byte[] buffer, uint count, out uint written, IntPtr overlapped);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern IntPtr Write(int fd, IntPtr buffer, UIntPtr count);

    private sealed class TrackingWriter(TextWriter inner) : TextWriter
    {
        public override Encoding Encoding => inner.Encoding;

        [AllowNull]
        public override string NewLine
        {
            get => inner.NewLine;
            set => inner.NewLine = value ?? Environment.NewLine;
        }

        public override void Write(char value)
        {
            _written = true;
            inner.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            _written |= count > 0;
            inner.Write(buffer, index, count);
        }

        public override void Write(string? value)
        {
            _written |= !string.IsNullOrEmpty(value);
            inner.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _written = true;
            inner.WriteLine(value);
        }

        public override void WriteLine()
        {
            _written = true;
            inner.WriteLine();
        }

        public override void Flush()
        {
            inner.Flush();
        }
    }
}
