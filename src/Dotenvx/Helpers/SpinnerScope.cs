using System;
using System.CommandLine;

using Dotenvx.Interop.DotenvxJs.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Helpers;

/// <remarks>
/// `const spinner = await createSpinner({ ...spinnerOptions, ...options, text })` ... `if (spinner) spinner.stop()` of the
/// upstream actions: the spinner exists only on a terminal and without -q/-v/-d, and stops before the first output (the
/// logger stops it) or when the action ends.
/// </remarks>
public sealed class SpinnerScope : IDisposable
{
    private readonly ISpinner? _spinner;
    private bool _stopped;

    private SpinnerScope(ISpinner? spinner)
    {
        _spinner = spinner;
        if (spinner is not null)
        {
            // output written while the spinner draws stops it first (once)
            Spinners.Active = new StopAdapter(this);
        }
    }

    private void StopOnce()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _spinner?.Stop();
    }

    private sealed class StopAdapter(SpinnerScope scope) : ISpinner
    {
        public string Text { get; set; } = "";
        public void Start() { }
        public void Stop() => scope.StopOnce();
    }

    /// <summary>
    /// helpers/statusReporter.js: the text goes to the spinner; without one (quiet, no terminal) it is written to stderr, because
    /// approval instructions must stay visible when spinner output is disabled.
    /// </summary>
    public Action<string> OnStatus => text =>
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_spinner is not null)
        {
            _spinner.Text = text;
        }
        else
        {
            Stderr(text);
        }
    };

    /// <summary>`if (spinner) spinner.text = text`: progress text that is shown only on a spinner (init and spec scanning).</summary>
    public Action<string> OnText => text =>
    {
        if (_spinner is not null)
        {
            _spinner.Text = text;
        }
    };

    /// <summary>`statusReporter(null)`: every status text on stderr.</summary>
    public static Action<string> Stderr { get; } = text =>
    {
        if (!string.IsNullOrEmpty(text))
        {
            Console.Error.Write(text + "\n");
        }
    };

    public void Dispose()
    {
        Spinners.StopActive();
        StopOnce();
    }

    public static SpinnerScope Start(ParseResult parseResult, string text, string[]? frames = null)
    {
        var root = parseResult.RootCommandResult.Command as LoggingRootCommand;
        bool quiet = root?.IsQuiet(parseResult) == true;
        bool verbose = root?.IsVerboseFlag(parseResult) == true;
        bool debug = root?.IsDebugFlag(parseResult) == true;

        return new SpinnerScope(Spinners.CreateSpinner(new SpinnerOptions(null, quiet, verbose, debug, text, frames)));
    }
}
