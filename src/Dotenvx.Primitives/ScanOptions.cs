namespace Dotenvx.Primitives;

public sealed class ScanOptions
{
    public string[]? Ik { get; init; }
    public string[]? Ek { get; init; }
    public bool ExpandDoubleQuotedNewlines { get; init; } = true;
    public bool ConvertWindowsNewlines { get; init; } = true;
}

public sealed record ScanResult(Dictionary<string, List<string>> Parsed, Dictionary<string, List<string?>> Comments);

/// <remarks>
/// The full `scan(source, options, transform)` export (checked live): `ek` excludes keys, a non-empty `ik`
/// keeps only matching keys (exclusions win), the optional transform receives each entry and its return value
/// is what is stored in `Parsed`; comments are kept in parallel arrays (`null` where a line has none).
/// </remarks>
public static partial class Primitives
{
    public static ScanResult Scan(string? src, ScanOptions options, Func<ScanEntry, string>? transform = null)
    {
        string[] ik = options.Ik ?? [];
        string[] ek = options.Ek ?? [];
        Func<string, bool> included = Match(ik, ek);
        Func<string, bool> excluded = Match(ek);

        Dictionary<string, List<string>> parsed = [];
        Dictionary<string, List<string?>> comments = [];
        foreach (ScanEntry entry in Scan(src, options.ExpandDoubleQuotedNewlines, options.ConvertWindowsNewlines))
        {
            if (excluded(entry.Key) || (ik.Length > 0 && !included(entry.Key)))
            {
                continue;
            }

            string value = transform is null ? entry.Value : transform(entry);
            if (!parsed.TryGetValue(entry.Key, out List<string>? values))
            {
                parsed[entry.Key] = values = [];
                comments[entry.Key] = [];
            }

            values.Add(value);
            comments[entry.Key].Add(entry.Comment);
        }

        return new ScanResult(parsed, comments);
    }
}
