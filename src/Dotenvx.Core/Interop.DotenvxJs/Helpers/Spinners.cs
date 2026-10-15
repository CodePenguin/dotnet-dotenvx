using Dotenvx.Interop.YoctoSpinnerJs;

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
    /// <summary>createSpinner.js `FRAMES`.</summary>
    private static readonly string[] DefaultFrames = ["◇", "⬖", "◆", "⬗"];

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

    /// <summary>The yocto-spinner port behind the <see cref="ISpinner" /> the actions use.</summary>
    private sealed class YoctoSpinnerAdapter(YoctoSpinner inner) : ISpinner
    {
        public string Text
        {
            get => inner.Text;
            set => inner.Text = value;
        }

        public void Start() => inner.Start();

        public void Stop() => inner.Stop();
    }

    private static ISpinner? Create(SpinnerOptions options)
    {
        bool enabled = IsStderrTty() && options.Spinner != false && !options.Quiet && !options.Verbose && !options.Debug;
        if (!enabled)
        {
            return null;
        }

        var spinner = new YoctoSpinnerAdapter(new YoctoSpinner(options.Text ?? "thinking", options.Frames ?? DefaultFrames));
        spinner.Start();
        Active = spinner;

        return spinner;
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
