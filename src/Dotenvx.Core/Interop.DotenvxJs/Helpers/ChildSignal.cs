using System.Runtime.InteropServices;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Tells a child that was killed by a signal from one that exited with code 128+N, which <c>Process.ExitCode</c> reports the same way on
/// Unix. Node's execa does tell them apart (a signal leaves <c>exitCode</c> undefined, so dotenvx exits 1 and, except for SIGINT and
/// SIGTERM, reports <c>Command was killed with SIGHUP (Terminal closed): ...</c>); a child that exits with 143 passes 143 through.
/// <c>waitid(WEXITED | WNOWAIT)</c> peeks at the exit status without reaping the child, so <c>Process</c> still collects it afterwards.
/// If the runtime reaps the child first the peek fails and the plain exit code is used.
/// </remarks>
internal static class ChildSignal
{
    private const int PPid = 1;
    private const int WExited = 4;
    private const int CldKilled = 2;
    private const int CldDumped = 3;

    private static readonly string[] LinuxNames =
    [
        "", "SIGHUP", "SIGINT", "SIGQUIT", "SIGILL", "SIGTRAP", "SIGABRT", "SIGBUS", "SIGFPE", "SIGKILL", "SIGUSR1", "SIGSEGV", "SIGUSR2", "SIGPIPE",
        "SIGALRM", "SIGTERM", "SIGSTKFLT", "SIGCHLD", "SIGCONT", "SIGSTOP", "SIGTSTP", "SIGTTIN", "SIGTTOU", "SIGURG", "SIGXCPU", "SIGXFSZ",
        "SIGVTALRM", "SIGPROF", "SIGWINCH", "SIGIO", "SIGPWR", "SIGSYS"
    ];

    private static readonly string[] MacNames =
    [
        "", "SIGHUP", "SIGINT", "SIGQUIT", "SIGILL", "SIGTRAP", "SIGABRT", "SIGEMT", "SIGFPE", "SIGKILL", "SIGBUS", "SIGSEGV", "SIGSYS", "SIGPIPE",
        "SIGALRM", "SIGTERM", "SIGURG", "SIGSTOP", "SIGTSTP", "SIGCONT", "SIGCHLD", "SIGTTIN", "SIGTTOU", "SIGIO", "SIGXCPU", "SIGXFSZ",
        "SIGVTALRM", "SIGPROF", "SIGWINCH", "SIGINFO", "SIGUSR1", "SIGUSR2"
    ];

    // human-signals descriptions, as execa puts them in its message
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["SIGHUP"] = "Terminal closed",
        ["SIGINT"] = "User interruption with CTRL-C",
        ["SIGQUIT"] = "User interruption with CTRL-\\",
        ["SIGILL"] = "Invalid machine instruction",
        ["SIGTRAP"] = "Debugger breakpoint",
        ["SIGABRT"] = "Aborted",
        ["SIGBUS"] = "Bus error due to misaligned, non-existing address or paging error",
        ["SIGEMT"] = "Command should be emulated but is not implemented",
        ["SIGFPE"] = "Floating point arithmetic error",
        ["SIGKILL"] = "Forced termination",
        ["SIGUSR1"] = "Application-specific signal",
        ["SIGSEGV"] = "Segmentation fault",
        ["SIGUSR2"] = "Application-specific signal",
        ["SIGPIPE"] = "Broken pipe or socket",
        ["SIGALRM"] = "Timeout or timer",
        ["SIGTERM"] = "Termination",
        ["SIGSTKFLT"] = "Stack is empty or overflowed",
        ["SIGCHLD"] = "Child process terminated, paused or unpaused",
        ["SIGCONT"] = "Unpaused",
        ["SIGSTOP"] = "Paused",
        ["SIGTSTP"] = "Paused using CTRL-Z or \"suspend\"",
        ["SIGTTIN"] = "Background process cannot read terminal input",
        ["SIGTTOU"] = "Background process cannot write to terminal output",
        ["SIGURG"] = "Socket received out-of-band data",
        ["SIGXCPU"] = "Process timed out",
        ["SIGXFSZ"] = "File too big",
        ["SIGVTALRM"] = "Timeout or timer",
        ["SIGPROF"] = "Timeout or timer",
        ["SIGWINCH"] = "Terminal window size changed",
        ["SIGIO"] = "I/O is available",
        ["SIGINFO"] = "Request for process information",
        ["SIGPWR"] = "Device running out of power",
        ["SIGSYS"] = "Invalid system call"
    };

    [DllImport("libc", SetLastError = true)]
    private static extern int waitid(int idtype, int id, byte[] info, int options);

    /// <summary>The description execa prints after the signal name (`Terminal closed`), or null for an unknown signal.</summary>
    public static string? Describe(string signal) => Descriptions.GetValueOrDefault(signal);

    /// <summary>
    /// Starts watching a freshly started child. The task ends with the name of the signal that killed it (`SIGKILL`), or null when it
    /// exited normally or the status could not be read.
    /// </summary>
    public static Task<string?> WatchAsync(int pid)
    {
        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult<string?>(null);
        }

        return Task.Factory.StartNew(() => Wait(pid), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private static string? Wait(int pid)
    {
        try
        {
            bool mac = OperatingSystem.IsMacOS();
            int wnowait = mac ? 0x20 : 0x01000000;
            byte[] info = new byte[128];

            while (waitid(PPid, pid, info, WExited | wnowait) != 0)
            {
                if (Marshal.GetLastWin32Error() != 4 /*EINTR*/)
                {
                    return null;
                }
            }

            int code = BitConverter.ToInt32(info, 8);
            if (code != CldKilled && code != CldDumped)
            {
                return null;
            }

            int number = BitConverter.ToInt32(info, mac ? 20 : 24);
            string[] names = mac ? MacNames : LinuxNames;

            return number > 0 && number < names.Length ? names[number] : null;
        }
        catch (Exception)
        {
            return null; // libc not reachable: fall back to the exit code
        }
    }
}
