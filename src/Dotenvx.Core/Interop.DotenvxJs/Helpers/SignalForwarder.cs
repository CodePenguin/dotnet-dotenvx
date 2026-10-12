using System.Diagnostics;
using System.Runtime.InteropServices;

using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/executeCommand.js (the signal handling around the child process):
/// <list type="bullet">
/// <item>SIGINT on a terminal: the first one is not forwarded (the child already got it from the terminal), the second sends
/// SIGTERM and queues a SIGKILL one second later; without a terminal SIGINT is forwarded after a one second grace period.</item>
/// <item>SIGTERM is forwarded after the same grace period.</item>
/// <item>SIGHUP, SIGQUIT, SIGUSR1 and SIGUSR2 are forwarded immediately. (Upstream also lists SIGILL, SIGTRAP, SIGABRT, SIGBUS,
/// SIGFPE and SIGSEGV; the .NET runtime cannot hand those to managed code, so they keep their default action.)</item>
/// </list>
/// Unix only - Windows has no such signals and keeps the console Ctrl+C handler in <c>ExecuteCommand</c>. This class was
/// written against the Node source and the POSIX signal numbers but could not be exercised on the Windows development
/// machine; its decision logic is covered by unit tests through the <c>sendSignal</c> seam.
/// </remarks>
internal sealed class SignalForwarder : IDisposable
{
    public const int ForwardSignalGraceMs = 1000;
    public const int ForceKillGraceMs = 1000;

    private readonly Func<string, bool> _isChildRunning;
    private readonly bool _interactiveTty;
    private readonly IDotenvxLogger _logger;
    private readonly List<PosixSignalRegistration> _registrations = [];
    private readonly Action<string> _sendSignal;
    private readonly Action<string> _setSignalSent;
    private readonly HashSet<Timer> _timers = [];
    private int _sigintCount;

    /// <param name="isChildRunning">True while the child has neither exited nor been signalled to death.</param>
    /// <param name="sendSignal">`child.kill(signal)` by POSIX name ("SIGTERM", "SIGKILL", ...).</param>
    /// <param name="setSignalSent">Records which signal dotenvx sent (so its death is not reported as a failure).</param>
    public SignalForwarder(IDotenvxLogger logger, bool interactiveTty, Func<string, bool> isChildRunning,
        Action<string> sendSignal, Action<string> setSignalSent)
    {
        _logger = logger;
        _interactiveTty = interactiveTty;
        _isChildRunning = isChildRunning;
        _sendSignal = sendSignal;
        _setSignalSent = setSignalSent;
    }

    /// <summary>Registers the OS signal handlers (Unix only).</summary>
    public void Register()
    {
        Add(PosixSignal.SIGINT, "SIGINT", HandleSigint);
        Add(PosixSignal.SIGTERM, "SIGTERM", () => QueueSignalForward("SIGTERM"));

        foreach ((string name, int number) in OtherSignals())
        {
            Add((PosixSignal)number, name, () => HandleOtherSignal(name));
        }
    }

    private void Add(PosixSignal signal, string name, Action handler)
    {
        try
        {
            _registrations.Add(PosixSignalRegistration.Create(signal, context =>
            {
                context.Cancel = true; // a handler suppresses the default action, like process.on(signal) in Node
                _logger.Debug($"received {name}");
                handler();
            }));
        }
        catch (Exception)
        {
            // a signal the runtime cannot register keeps its default action
        }
    }

    private static IEnumerable<(string Name, int Number)> OtherSignals()
    {
        bool mac = OperatingSystem.IsMacOS();

        return
        [
            ("SIGHUP", 1), ("SIGQUIT", 3), ("SIGUSR1", mac ? 30 : 10), ("SIGUSR2", mac ? 31 : 12)
        ];
    }

    public void HandleSigint()
    {
        _sigintCount++;

        if (_interactiveTty)
        {
            if (_sigintCount == 1)
            {
                _logger.Debug("TTY mode: not forwarding first SIGINT to command process");

                return;
            }

            _logger.Debug("TTY mode: forwarding SIGTERM on second SIGINT to command process");
            _setSignalSent("SIGTERM");
            _sendSignal("SIGTERM");

            if (_sigintCount == 2)
            {
                QueueForceKill();
            }

            return;
        }

        QueueSignalForward("SIGINT");
    }

    public void HandleOtherSignal(string signal)
    {
        _logger.Debug($"received {signal}");
        _sendSignal(signal);
    }

    public void QueueSignalForward(string signal)
    {
        _logger.Debug($"queueing {signal} to command process after {ForwardSignalGraceMs}ms");
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            Forget(timer!);

            if (!_isChildRunning(signal))
            {
                _logger.Debug($"skipping {signal} forward because command process is already exiting");

                return;
            }

            _logger.Debug($"sending {signal} to command process");
            _setSignalSent(signal);
            _sendSignal(signal);
        }, null, ForwardSignalGraceMs, Timeout.Infinite);
        Remember(timer);
    }

    private void QueueForceKill()
    {
        _logger.Debug($"queueing SIGKILL to command process after {ForceKillGraceMs}ms");
        Timer? timer = null;
        timer = new Timer(_ =>
        {
            Forget(timer!);

            if (!_isChildRunning("SIGKILL"))
            {
                _logger.Debug("skipping SIGKILL because command process is already exiting");

                return;
            }

            _logger.Debug("sending SIGKILL to command process");
            _sendSignal("SIGKILL");
        }, null, ForceKillGraceMs, Timeout.Infinite);
        Remember(timer);
    }

    private void Remember(Timer timer)
    {
        lock (_timers)
        {
            _timers.Add(timer);
        }
    }

    private void Forget(Timer timer)
    {
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    public void Dispose()
    {
        foreach (PosixSignalRegistration registration in _registrations)
        {
            registration.Dispose();
        }

        lock (_timers)
        {
            foreach (Timer timer in _timers)
            {
                timer.Dispose();
            }

            _timers.Clear();
        }
    }

    // ---- sending a real signal to a process ----

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    /// <summary>`child.kill(signal)`: a POSIX signal by name, to the process.</summary>
    public static void Send(Process process, string signal)
    {
        int number = signal switch
        {
            "SIGHUP" => 1,
            "SIGINT" => 2,
            "SIGQUIT" => 3,
            "SIGKILL" => 9,
            "SIGUSR1" => OperatingSystem.IsMacOS() ? 30 : 10,
            "SIGUSR2" => OperatingSystem.IsMacOS() ? 31 : 12,
            "SIGTERM" => 15,
            _ => 15
        };

        try
        {
            if (!process.HasExited)
            {
                Kill(process.Id, number);
            }
        }
        catch (InvalidOperationException)
        {
            // the process is already gone
        }
    }
}
