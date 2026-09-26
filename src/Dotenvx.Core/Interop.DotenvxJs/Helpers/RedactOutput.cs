namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/redactOutput.js. Upstream's `redactOutput` recurses
/// over arrays/plain-objects/strings; this port only ever needs a plain-string overload (for
/// per-line debug/error messages) and a string-dictionary overload (for the parsed/injected/
/// existed dumps `run` logs at debug level) - no call site in this port passes anything else, so
/// the array/object recursion isn't ported. `createRedactedStreamWriter`'s `StringDecoder`-based
/// byte-chunk decoding is also not ported: .NET's `StreamReader`/`TextReader.ReadAsync(char[])`
/// already decodes UTF-8 byte chunks into text before this code ever sees them, so there's no raw-
/// byte multi-byte-split concern left to guard against - only the partial-sensitive-value holdback
/// buffering (this file's actual reason for existing) still applies and is ported below.
/// </remarks>
public static partial class Helpers
{
    private static string[] NormalizedValues(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct()
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    public static string? RedactOutputText(string? value, IReadOnlyList<string> sensitiveValues)
    {
        string[] values = NormalizedValues(sensitiveValues);
        if (values.Length < 1 || value is null)
        {
            return value;
        }

        string result = value;
        foreach (string sensitiveValue in values)
        {
            result = result.Replace(sensitiveValue, Redact(sensitiveValue));
        }

        return result;
    }

    public static Dictionary<string, string> RedactOutputDict(
        IReadOnlyDictionary<string, string> value, IReadOnlyList<string> sensitiveValues)
    {
        Dictionary<string, string> result = [];
        foreach ((string key, string item) in value)
        {
            result[key] = RedactOutputText(item, sensitiveValues) ?? item;
        }

        return result;
    }

    private static int PartialMatchLength(string value, IReadOnlyList<string> sensitiveValues)
    {
        int longest = 0;

        foreach (string sensitiveValue in sensitiveValues)
        {
            int maxLength = Math.Min(value.Length, sensitiveValue.Length - 1);
            for (int length = maxLength; length > longest; length--)
            {
                if (sensitiveValue.StartsWith(value[^length..], StringComparison.Ordinal))
                {
                    longest = length;
                    break;
                }
            }
        }

        return longest;
    }

    private static int SafeBoundary(string value, int boundary, IReadOnlyList<string> sensitiveValues)
    {
        int result = boundary;
        bool changed = true;

        while (changed)
        {
            changed = false;
            foreach (string sensitiveValue in sensitiveValues)
            {
                int index = value.IndexOf(sensitiveValue, StringComparison.Ordinal);
                while (index != -1)
                {
                    int end = index + sensitiveValue.Length;
                    if (index < result && end > result)
                    {
                        result = index;
                        changed = true;
                    }

                    index = value.IndexOf(sensitiveValue, index + 1, StringComparison.Ordinal);
                }
            }
        }

        return result;
    }

    /// <remarks>
    /// Reference source: dotenvx/src/lib/helpers/redactOutput.js's `createRedactedStreamWriter` -
    /// backpressure-aware pause/resume of the source stream isn't ported (see this file's class-
    /// level remark); the read loop that drives this writer already applies natural backpressure
    /// by not reading the next chunk until the previous one has been written.
    /// </remarks>
    internal sealed class RedactedStreamWriter(TextWriter output, IReadOnlyList<string> sensitiveValues)
    {
        private string _pending = string.Empty;

        public void Write(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            _pending += chunk;

            int holdbackLength = PartialMatchLength(_pending, sensitiveValues);
            int boundary = _pending.Length - holdbackLength;
            boundary = SafeBoundary(_pending, boundary, sensitiveValues);

            string toWrite = _pending[..boundary];
            _pending = _pending[boundary..];

            WriteToStream(toWrite);
        }

        public void Flush()
        {
            if (_pending.Length == 0)
            {
                return;
            }

            WriteToStream(_pending);
            _pending = string.Empty;
        }

        private void WriteToStream(string value)
        {
            if (value.Length == 0)
            {
                return;
            }

            output.Write(RedactOutputText(value, sensitiveValues));
        }
    }
}
