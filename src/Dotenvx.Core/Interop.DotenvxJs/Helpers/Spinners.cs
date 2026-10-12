using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>The slice of yocto-spinner the actions use.</summary>
public interface ISpinner
{
    /// <summary>The text next to the frame; assigning redraws.</summary>
    string Text { get; set; }

    void Start();

    void Stop();
}

/// <summary>`createSpinner(options)`'s input: the global flags plus the text and frames of this command.</summary>
public record SpinnerOptions(
    bool? Spinner = null, bool Quiet = false, bool Verbose = false, bool Debug = false, string? Text = null,
    string[]? Frames = null);

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/createSpinner.js (a yocto-spinner on stderr, only on a terminal and never with
/// `--quiet`, `--verbose` or `--debug`) and the `yocto-spinner` package behind it. Non-terminal runs (every scripted and
/// parity-tested use) get null, exactly like upstream.
/// </remarks>
public static class Spinners
{
    private static readonly string[] DefaultFrames = OperatingSystem.IsWindows() &&
                                                     string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")) &&
                                                     Environment.GetEnvironmentVariable("TERM_PROGRAM") != "vscode"
        ? ["-", "\\", "|", "/"]
        : ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private static readonly Func<SpinnerOptions, ISpinner?> DefaultCreate = Create;

    /// <summary>`createSpinner`. Swappable so action tests can observe the spinner.</summary>
    public static Func<SpinnerOptions, ISpinner?> CreateSpinner { get; internal set; } = DefaultCreate;

    /// <summary>The spinner currently drawing, so anything written to the terminal can stop it first (upstream stops before output).</summary>
    public static ISpinner? Active { get; set; }

    /// <summary>`createSpinner.pause()`: stops drawing while something else (a prompt) owns the terminal. Swappable for tests.</summary>
    public static Action Pause { get; set; } = () => Active?.Stop();

    /// <summary>`createSpinner.resume()`.</summary>
    public static Action Resume { get; set; } = () => Active?.Start();

    /// <summary>Stops the active spinner, if any.</summary>
    public static void StopActive()
    {
        ISpinner? active = Active;
        Active = null;
        active?.Stop();
    }

    /// <summary>`process.stderr.isTTY`. Swappable for tests.</summary>
    public static Func<bool> IsStderrTty { get; internal set; } = () => !Console.IsErrorRedirected;

    internal static void ResetToDefaults()
    {
        CreateSpinner = DefaultCreate;
        Active = null;
        Pause = () => Active?.Stop();
        Resume = () => Active?.Start();
        IsStderrTty = () => !Console.IsErrorRedirected;
    }

    private static ISpinner? Create(SpinnerOptions options)
    {
        bool enabled = IsStderrTty() && options.Spinner != false && !options.Quiet && !options.Verbose && !options.Debug;
        if (!enabled)
        {
            return null;
        }

        var spinner = new ConsoleSpinner(options.Text ?? "thinking", options.Frames ?? DefaultFrames);
        spinner.Start();
        Active = spinner;

        return spinner;
    }

    /// <summary>yocto-spinner: redraws `frame text` every 80ms on one stderr line (a plain line per update when not interactive).</summary>
    private sealed class ConsoleSpinner : ISpinner
    {
        private const int IntervalMs = 80;
        private readonly string[] _frames;
        private readonly bool _interactive;
        private readonly object _lock = new();
        private int _frame = -1;
        private bool _spinning;
        private Timer? _timer;
        private string _text;

        public ConsoleSpinner(string text, string[] frames)
        {
            _text = text;
            _frames = frames;
            // yocto-spinner: isTTY && TERM !== 'dumb' && !('CI' in process.env)
            _interactive = Environment.GetEnvironmentVariable("TERM") != "dumb" &&
                           Environment.GetEnvironmentVariable("CI") is null;
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                Render();
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_spinning)
                {
                    return;
                }

                _spinning = true;
                if (_interactive)
                {
                    Write("\u001b[?25l");
                }

                Render();
                if (_interactive)
                {
                    _timer = new Timer(_ => Render(), null, IntervalMs, IntervalMs);
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_spinning)
                {
                    return;
                }

                _spinning = false;
                _timer?.Dispose();
                _timer = null;
                if (_interactive)
                {
                    Write("\r\u001b[2K");
                    Write("\u001b[?25h");
                }
            }
        }

        private void Render()
        {
            lock (_lock)
            {
                if (!_spinning)
                {
                    return;
                }

                _frame = (_frame + 1) % _frames.Length;
                string line = $"\u001b[36m{_frames[_frame]}\u001b[39m {_text}";
                Write(_interactive ? "\r\u001b[2K" + line : line + "\n");
            }
        }

        private static void Write(string text)
        {
            Console.Error.Write(text);
        }
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/createElapsedStatus.js</remarks>
public static class ElapsedStatus
{
    /// <summary>
    /// Reports <paramref name="text" /> now and `text (Ns)` every second until the returned stop action runs. With no
    /// callback nothing happens. <paramref name="timeProvider" /> is the clock (a fake one in tests).
    /// </summary>
    public static Action Create(Action<string>? onStatus, string text, TimeProvider? timeProvider = null)
    {
        if (onStatus is null)
        {
            return () => { };
        }

        int elapsed = 0;
        onStatus(text);

        ITimer timer = (timeProvider ?? TimeProvider.System).CreateTimer(_ =>
        {
            elapsed++;
            onStatus($"{text} ({elapsed}s)");
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        return () => timer.Dispose();
    }
}
