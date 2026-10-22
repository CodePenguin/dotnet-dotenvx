using System.Text;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference sources: dotenvx/src/lib/envfile/validation/diagnosticLocation.js and
/// envfile/rendering/previewEnvfile.js (where each checked value came from, and the `check` preview table).
/// </remarks>
internal static partial class EnvfileDiagnostics
{
    // Match the assignment grammar used by primitives.scan, including multiline values.
    [GeneratedRegex(
        @"^\s*(?:export\s+)?([\w.-]+)(?:\s*=\s*?|:\s+?)(\s*'(?:\\'|[^'])*'|\s*""(?:\\""|[^""])*""|\s*`(?:\\`|[^`])*`|[^#\r\n]+)?\s*(?:#(.*))?$",
        RegexOptions.Multiline)]
    private static partial Regex AssignmentRegex();

    private static Dictionary<string, int> AssignmentLines(string? src)
    {
        string text = Regex.Replace(src ?? "", "\r\n?", "\n");
        Dictionary<string, int> result = [];
        foreach (Match match in AssignmentRegex().Matches(text))
        {
            int offset = 0;
            while (offset < match.Value.Length && char.IsWhiteSpace(match.Value[offset]))
            {
                offset++;
            }

            int start = match.Index + offset;
            result[match.Groups[1].Value] = text[..start].Count(c => c == '\n') + 1;
        }

        return result;
    }

    public static Func<string, string> Locations(IEnumerable<RunProcessedEnv> processedEnvs, string fallback,
        string? environmentLabel = null)
    {
        Dictionary<string, string> locations = [];
        foreach (RunProcessedEnv row in processedEnvs)
        {
            string filename = row.Type == EnvType.EnvFile
                ? Path.GetRelativePath(Directory.GetCurrentDirectory(), NodePath.Resolve(row.FilePath))
                : string.IsNullOrEmpty(environmentLabel) ? "--env" : environmentLabel;
            Dictionary<string, int> lines = AssignmentLines(string.IsNullOrEmpty(row.Src) ? row.String : row.Src);
            foreach (string key in row.Injected.Keys)
            {
                int line = 0;
                if (row.Type == EnvType.EnvFile || string.IsNullOrEmpty(environmentLabel))
                {
                    lines.TryGetValue(key, out line);
                }

                locations[key] = line > 0 ? $"{filename}:{line}" : filename;
            }

            foreach (string key in row.PreExisted.Keys.Where(key => !locations.ContainsKey(key)))
            {
                locations[key] = string.IsNullOrEmpty(environmentLabel) ? "shell environment" : environmentLabel;
            }
        }

        return key => locations.TryGetValue(key, out string? value) && value != "" ? value : fallback;
    }

    [GeneratedRegex(@"[\s#""'`\\\u007f-\u009f]")]
    private static partial Regex NeedsQuoting();

    [GeneratedRegex(@"[\u007f-\u009f

]")]
    private static partial Regex Escapable();

    private static string DisplayValue(string value)
    {
        bool hasControl = value.Any(c => c < 32);
        if (value != "" && !hasControl && !NeedsQuoting().IsMatch(value))
        {
            return value;
        }

        return Escapable().Replace(Helpers.JsonStringify(value), m => $"\\u{(int)m.Value[0]:x4}");
    }

    /// <summary>Shows final resolved values once, not source contents or unrelated shell variables.</summary>
    public static string Preview(IReadOnlyList<RunProcessedEnv> processedEnvs, EnvfileSchema schema,
        IReadOnlyDictionary<string, string> checkedValues, IReadOnlyList<CheckError> diagnostics)
    {
        List<string> keys = [];
        void Add(string key)
        {
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        foreach (RunProcessedEnv row in processedEnvs)
        {
            foreach (string key in row.Parsed.Keys.Concat(row.Injected.Keys).Concat(row.PreExisted.Keys))
            {
                Add(key);
            }
        }

        foreach (string key in schema.Keys.Keys)
        {
            Add(key);
        }

        Func<string, string> location = Locations(processedEnvs, "process.env", "process.env");
        Dictionary<string, List<string>> errors = [];
        foreach (CheckError diagnostic in diagnostics)
        {
            Add(diagnostic.Key);
            string message = !string.IsNullOrEmpty(diagnostic.Rule) ? $"!{diagnostic.Rule}" : $"! {diagnostic.Message}";
            if (!errors.TryGetValue(diagnostic.Key, out List<string>? list))
            {
                errors[diagnostic.Key] = list = [];
            }

            if (!list.Contains(message))
            {
                list.Add(message);
            }
        }

        List<(string Assignment, string Comment, string Error, bool Unspecced)> rows = [];
        foreach (string key in keys)
        {
            bool missing = !checkedValues.ContainsKey(key);
            if (Regex.IsMatch(key, "^DOTENV_(?:PUBLIC|PRIVATE)_KEY") || (missing && !errors.ContainsKey(key)))
            {
                continue;
            }

            string value = missing ? "" : checkedValues[key];
            string display = missing ? "[MISSING]" : DisplayValue(Primitives.Primitives.Encrypted(value) ? "[REDACTED]" : value);
            string source = missing ? "" : DisplayValue(location(key));
            string error = errors.TryGetValue(key, out List<string>? messages) ? string.Join(", ", messages) : "";
            bool unspecced = !schema.Keys.ContainsKey(key);
            string status = string.Join(", ", new[] { unspecced ? "!declared" : "", error }.Where(s => s != ""));
            rows.Add(($"{key}={display}", string.Join(' ', new[] { source, status }.Where(s => s != "")), error, unspecced));
        }

        int width = rows.Count == 0 ? 0 : rows.Max(row => row.Assignment.Length);
        StringBuilder builder = new();
        List<string> lines = [];
        foreach ((string assignment, string comment, string error, bool unspecced) in rows)
        {
            string line = $"{assignment.PadRight(width)} # {comment}";
            lines.Add(error != ""
                ? Shared.Shared.GetColor("red", ColorStream.StdOut)($"☠ {line}")
                : unspecced
                    ? Shared.Shared.GetColor("orangered", ColorStream.StdOut)($"⚠ {line}")
                    : $"{Shared.Shared.GetColor("green", ColorStream.StdOut)("✔")} {Shared.Shared.GetColor("gray", ColorStream.StdOut)(line)}");
        }

        return builder.AppendJoin('\n', lines).ToString();
    }
}
