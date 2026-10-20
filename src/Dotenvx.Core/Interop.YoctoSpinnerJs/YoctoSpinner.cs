using System.Text;

namespace Dotenvx.Interop.YoctoSpinnerJs;

/// <summary>
/// Reference source: the yocto-spinner npm package (index.js). Redraws `frame text` every 80ms on the stderr lines it owns (a plain line per update when not
/// interactive), clearing what it drew before each redraw, inside synchronized-output markers.
/// </summary>
public sealed class YoctoSpinner
{
    private const int IntervalMs = 80;
    private readonly string[] _frames;
    private readonly bool _interactive;
    private readonly object _lock = new();
    private int _frame = -1;
    private long _lastFrameTime;
    private int _lines;
    private bool _deferringRender;
    private TextWriter? _originalOut;
    private TextWriter? _originalError;
    private readonly TextWriter _stderr = Console.Error;
    private bool _spinning;
    private Timer? _timer;
    private string _text;

    public YoctoSpinner(string text, string[] frames)
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
            lock (_lock)
            {
                _text = value ?? "";
                Render();
            }
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
                InstallHook();
            }

            Render();
            if (_interactive)
            {
                _timer = new Timer(_ =>
                {
                    lock (_lock)
                    {
                        Render();
                    }
                }, null, IntervalMs, IntervalMs);
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
            _deferringRender = false;
            if (_interactive)
            {
                UninstallHook();
                Write("\u001b[?25h");
            }

            Clear();
        }
    }

    /// <summary>`clear()`: back to column 0, then wipe each line the last frame took.</summary>
    private void Clear()
    {
        if (!_interactive || _lines == 0)
        {
            return;
        }

        var builder = new StringBuilder("\u001b[1G");
        for (int index = 0; index < _lines; index++)
        {
            if (index > 0)
            {
                builder.Append("\u001b[1A");
            }

            builder.Append("\u001b[2K");
        }

        Write(builder.ToString());
        _lines = 0;
    }

    private void Render()
    {
        if (!_spinning || _deferringRender)
        {
            return;
        }

        long now = Environment.TickCount64;
        if (_frame == -1 || now - _lastFrameTime >= IntervalMs)
        {
            _frame = (_frame + 1) % _frames.Length;
            _lastFrameTime = now;
        }

        string line = $"\u001b[36m{_frames[_frame]}\u001b[39m {_text}";
        if (!_interactive)
        {
            Write(line + "\n");

            return;
        }

        Write("\u001b[?2026h");
        Clear();
        Write(line);
        Write("\u001b[?2026l");
        _lines = LineCount(line);
    }

    /// <summary>`#lineCount`: terminal rows the (colour-stripped) text occupies at the stream's width.</summary>
    private static int LineCount(string text)
    {
        int width = 80;
        try
        {
            if (Console.WindowWidth > 0)
            {
                width = Console.WindowWidth;
            }
        }
        catch (IOException)
        {
        }

        string plain = System.Text.RegularExpressions.Regex.Replace(text, "\u001b\\[[0-9;?]*[A-Za-z]", "");
        int count = 0;
        foreach (string part in plain.Split('\n'))
        {
            count += Math.Max(1, (int)Math.Ceiling(part.Length / (double)width));
        }

        return count;
    }

    private void Write(string text)
    {
        _stderr.Write(text);
    }

    /// <summary>`#installHook`: output written to the terminal while spinning first wipes the spinner and redraws it after.</summary>
    private void InstallHook()
    {
        if (_originalError is not null)
        {
            return;
        }

        _originalError = Console.Error;
        Console.SetError(new HookedWriter(this, _originalError));
        if (!Console.IsOutputRedirected)
        {
            _originalOut = Console.Out;
            Console.SetOut(new HookedWriter(this, _originalOut));
        }
    }

    private void UninstallHook()
    {
        if (_originalError is not null)
        {
            Console.SetError(_originalError);
            _originalError = null;
        }

        if (_originalOut is not null)
        {
            Console.SetOut(_originalOut);
            _originalOut = null;
        }
    }

    /// <summary>`#hookedWrite`.</summary>
    private void Hooked(TextWriter original, string chunk)
    {
        lock (_lock)
        {
            if (!_spinning)
            {
                original.Write(chunk);

                return;
            }

            if (_lines > 0)
            {
                Clear();
            }

            original.Write(chunk);
            if (chunk.Length > 0 && chunk[^1] == '\n')
            {
                _deferringRender = false;
            }
            else if (chunk.Length > 0)
            {
                _deferringRender = true;
            }

            if (_spinning && !_deferringRender)
            {
                Render();
            }
        }
    }

    private sealed class HookedWriter(YoctoSpinner owner, TextWriter original) : TextWriter
    {
        public override System.Text.Encoding Encoding => original.Encoding;

        public override IFormatProvider FormatProvider => original.FormatProvider;

        public override string NewLine
        {
            get => original.NewLine;
            set => original.NewLine = value;
        }

        public override void Write(char value) => owner.Hooked(original, value.ToString());

        public override void Write(string? value) => owner.Hooked(original, value ?? string.Empty);

        public override void Write(char[] buffer, int index, int count) => owner.Hooked(original, new string(buffer, index, count));

        public override void Write(ReadOnlySpan<char> buffer) => owner.Hooked(original, new string(buffer));

        public override void WriteLine(string? value) => owner.Hooked(original, (value ?? string.Empty) + original.NewLine);

        public override void WriteLine() => owner.Hooked(original, original.NewLine);

        public override void Flush() => original.Flush();
    }
}
