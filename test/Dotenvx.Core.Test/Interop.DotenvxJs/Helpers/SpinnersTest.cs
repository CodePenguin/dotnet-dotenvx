using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/createSpinner.js and createElapsedStatus.js</remarks>
public class SpinnersTest : EnvironmentTestBase
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, period);
            _timers.Add(timer);

            return timer;
        }

        public void Advance(TimeSpan time)
        {
            foreach (ManualTimer timer in _timers.ToList())
            {
                timer.Advance(time);
            }
        }

        private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan period) : ITimer
        {
            private TimeSpan _accumulated;
            private bool _disposed;

            public void Advance(TimeSpan time)
            {
                _accumulated += time;
                while (!_disposed && _accumulated >= period)
                {
                    _accumulated -= period;
                    callback(state);
                }
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() => _disposed = true;

            public ValueTask DisposeAsync()
            {
                _disposed = true;

                return ValueTask.CompletedTask;
            }
        }
    }

    /// <remarks>Refer to "createSpinner returns null when disabled"</remarks>
    [Fact]
    public void TestCreateSpinnerReturnsNullWhenDisabled()
    {
        Spinners.IsStderrTty = () => false;

        Spinners.CreateSpinner(new SpinnerOptions()).ShouldBeNull();
    }

    /// <remarks>Refer to "createSpinner returns null when spinner option is false"</remarks>
    [Fact]
    public void TestCreateSpinnerReturnsNullWhenSpinnerOptionIsFalse()
    {
        Spinners.IsStderrTty = () => true;

        Spinners.CreateSpinner(new SpinnerOptions(Spinner: false)).ShouldBeNull();
    }

    /// <remarks>Refer to "createSpinner creates and starts spinner when enabled"</remarks>
    [Fact]
    public void TestCreateSpinnerCreatesAndStartsSpinnerWhenEnabled()
    {
        Spinners.IsStderrTty = () => true;
        TextWriter original = Console.Error;
        Console.SetError(new StringWriter());
        try
        {
            ISpinner? spinner = Spinners.CreateSpinner(new SpinnerOptions());

            spinner.ShouldNotBeNull();
            spinner.Stop();
        }
        finally
        {
            Console.SetError(original);
        }
    }

    /// <remarks>Not from a JS test - quiet/verbose/debug also disable the spinner (createSpinner.js).</remarks>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void TestCreateSpinnerIsOffForQuietVerboseAndDebug(bool quiet, bool verbose, bool debug)
    {
        Spinners.IsStderrTty = () => true;

        Spinners.CreateSpinner(new SpinnerOptions(Quiet: quiet, Verbose: verbose, Debug: debug)).ShouldBeNull();
    }

    /// <remarks>Refer to "reports elapsed seconds until stopped"</remarks>
    [Fact]
    public void TestReportsElapsedSecondsUntilStopped()
    {
        var clock = new ManualTimeProvider();
        var statuses = new List<string>();

        Action stop = ElapsedStatus.Create(statuses.Add, "awaiting 1password", clock);
        clock.Advance(TimeSpan.FromMilliseconds(2500));
        stop();
        clock.Advance(TimeSpan.FromMilliseconds(1000));

        statuses.ShouldBe(["awaiting 1password", "awaiting 1password (1s)", "awaiting 1password (2s)"]);
    }
}
