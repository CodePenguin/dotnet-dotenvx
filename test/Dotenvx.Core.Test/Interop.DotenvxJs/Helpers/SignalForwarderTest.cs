using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Not from a JS test (executeCommand.js marks its signal handlers `c8 ignore`) - the decision logic of
/// <see cref="SignalForwarder" /> (Reference source: dotenvx/src/lib/helpers/executeCommand.js) checked through its seams;
/// delivering real POSIX signals needs a Unix host.
/// </remarks>
public class SignalForwarderTest : EnvironmentTestBase
{
    private readonly List<string> _sent = [];
    private readonly List<string> _recorded = [];
    private bool _running = true;

    private SignalForwarder Forwarder(bool tty)
    {
        return new SignalForwarder(MockLogger, tty, _ => _running, _sent.Add, _recorded.Add);
    }

    [Fact]
    public void TheFirstSigintOnATerminalIsNotForwarded()
    {
        using SignalForwarder forwarder = Forwarder(true);

        forwarder.HandleSigint();

        _sent.ShouldBeEmpty();
        MockLogger.DebugMessages.ShouldContain("TTY mode: not forwarding first SIGINT to command process");
    }

    [Fact]
    public void TheSecondSigintOnATerminalSendsSigtermAndQueuesAForceKill()
    {
        using SignalForwarder forwarder = Forwarder(true);

        forwarder.HandleSigint();
        forwarder.HandleSigint();

        _sent.ShouldBe(["SIGTERM"]);
        _recorded.ShouldBe(["SIGTERM"]);
        Thread.Sleep(SignalForwarder.ForceKillGraceMs + 400);
        _sent.ShouldBe(["SIGTERM", "SIGKILL"]);
    }

    [Fact]
    public void AForceKillIsSkippedWhenTheChildAlreadyExited()
    {
        using SignalForwarder forwarder = Forwarder(true);

        forwarder.HandleSigint();
        forwarder.HandleSigint();
        _running = false;
        Thread.Sleep(SignalForwarder.ForceKillGraceMs + 400);

        _sent.ShouldBe(["SIGTERM"]);
        MockLogger.DebugMessages.ShouldContain("skipping SIGKILL because command process is already exiting");
    }

    [Fact]
    public void SigintWithoutATerminalIsForwardedAfterTheGracePeriod()
    {
        using SignalForwarder forwarder = Forwarder(false);

        forwarder.HandleSigint();

        _sent.ShouldBeEmpty("queued, not immediate");
        Thread.Sleep(SignalForwarder.ForwardSignalGraceMs + 400);
        _sent.ShouldBe(["SIGINT"]);
        _recorded.ShouldBe(["SIGINT"]);
    }

    [Fact]
    public void AQueuedForwardIsSkippedWhenTheChildAlreadyExited()
    {
        using SignalForwarder forwarder = Forwarder(false);

        forwarder.QueueSignalForward("SIGTERM");
        _running = false;
        Thread.Sleep(SignalForwarder.ForwardSignalGraceMs + 400);

        _sent.ShouldBeEmpty();
        MockLogger.DebugMessages.ShouldContain("skipping SIGTERM forward because command process is already exiting");
    }

    [Fact]
    public void OtherSignalsAreForwardedImmediately()
    {
        using SignalForwarder forwarder = Forwarder(false);

        forwarder.HandleOtherSignal("SIGHUP");

        _sent.ShouldBe(["SIGHUP"]);
        MockLogger.DebugMessages.ShouldContain("received SIGHUP");
    }

    [Fact]
    public void DisposingCancelsPendingForwards()
    {
        SignalForwarder forwarder = Forwarder(false);
        forwarder.QueueSignalForward("SIGTERM");

        forwarder.Dispose();
        Thread.Sleep(SignalForwarder.ForwardSignalGraceMs + 400);

        _sent.ShouldBeEmpty();
    }
}
