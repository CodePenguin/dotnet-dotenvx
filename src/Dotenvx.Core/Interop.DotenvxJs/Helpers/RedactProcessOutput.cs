using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactProcessOutput.js. Redacts everything this process itself writes to
/// stdout/stderr (logs, errors) once an Envfile is in play. A possible secret prefix is held back between writes; at exit an
/// unfinished prefix is masked rather than released.
/// </remarks>
public static class RedactProcessOutput
{
    private sealed class Redactor(TextWriter inner, List<string> secrets) : TextWriter
    {
        private readonly object _gate = new();
        private string _pending = "";

        public override Encoding Encoding => inner.Encoding;

        public void Add(IEnumerable<string> more)
        {
            lock (_gate)
            {
                foreach (string secret in more.Where(secret => !secrets.Contains(secret)))
                {
                    secrets.Add(secret);
                }
            }
        }

        private string Consume(string chunk)
        {
            _pending += chunk;
            string[] values = [.. secrets];
            int boundary = Helpers.SafeBoundary(_pending, _pending.Length - Helpers.PartialMatchLength(_pending, values), values);
            string output = global::Dotenvx.Primitives.Primitives.Redact(_pending[..boundary], values);
            _pending = _pending[boundary..];

            return output;
        }

        /// <summary>`flush(true)`: an unfinished secret prefix is masked, not released.</summary>
        public string Finish()
        {
            lock (_gate)
            {
                string output = _pending.Length > 0 ? "[REDACTED]" : "";
                _pending = "";

                return output;
            }
        }

        public override void Write(char value)
        {
            Write(value.ToString());
        }

        public override void Write(char[] buffer, int index, int count)
        {
            Write(new string(buffer, index, count));
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            lock (_gate)
            {
                string output = Consume(value);
                if (output.Length > 0)
                {
                    inner.Write(output);
                    inner.Flush();
                }
            }
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public void WriteRaw(string output)
        {
            if (output.Length > 0)
            {
                inner.Write(output);
                inner.Flush();
            }
        }
    }

    private static Redactor? _out;
    private static Redactor? _error;
    private static bool _exitHooked;
    private static TextWriter? _originalOut;
    private static TextWriter? _originalError;

    /// <summary>Test seam: puts the original console writers back and forgets every secret (the real process never uninstalls).</summary>
    internal static void Reset()
    {
        if (_out is null)
        {
            return;
        }

        Console.SetOut(_originalOut!);
        Console.SetError(_originalError!);
        _out = null;
        _error = null;
    }


    /// <summary>`redactProcessOutput(secrets)`.</summary>
    public static void Install(IEnumerable<string> secrets)
    {
        string[] values = [.. secrets.Where(value => !string.IsNullOrEmpty(value)).Distinct()];
        if (values.Length == 0)
        {
            return;
        }

        if (_out is not null && _error is not null)
        {
            _out.Add(values);
            _error.Add(values);

            return;
        }

        _originalOut = Console.Out;
        _originalError = Console.Error;
        _out = new Redactor(Console.Out, [.. values]);
        _error = new Redactor(Console.Error, [.. values]);
        Console.SetOut(_out);
        Console.SetError(_error);
        if (!_exitHooked)
        {
            _exitHooked = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                _out?.WriteRaw(_out.Finish());
                _error?.WriteRaw(_error.Finish());
            };
        }
    }
}
