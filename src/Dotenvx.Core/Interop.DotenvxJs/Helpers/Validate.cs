using System.Text.RegularExpressions;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public sealed record ValidationError(string Code, string[] Keys, string Message)
{
    // value equality over the key list, like the deep comparison the upstream tests make
    public bool Equals(ValidationError? other)
    {
        return other is not null && Code == other.Code && Message == other.Message && Keys.SequenceEqual(other.Keys);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Code, Message, Keys.Length);
    }
}

public sealed record ValidationResult(bool Valid, ValidationError[] Errors);

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/validate.js. Every key of the example must have a non-blank
/// value in the env unless one of its occurrences carries an inline `optional` comment (whole word,
/// case-insensitive); all missing keys are reported in a single MISSING_REQUIRED error.
/// </remarks>
public static partial class Helpers
{
    public static ValidationResult Validate(IReadOnlyDictionary<string, List<string>> example,
        IReadOnlyDictionary<string, string> env, IReadOnlyDictionary<string, List<string?>>? comments = null)
    {
        List<ValidationError> errors = [];
        List<string> missingRequired = [];
        HashSet<string> optional = OptionalKeys(comments ?? new Dictionary<string, List<string?>>());

        foreach (string key in example.Keys)
        {
            bool missing = !env.TryGetValue(key, out string? value) || JsTrim(value).Length == 0;
            if (!optional.Contains(key) && missing)
            {
                missingRequired.Add(key);
            }
        }

        if (missingRequired.Count > 0)
        {
            errors.Add(new ValidationError("MISSING_REQUIRED", [.. missingRequired],
                $"missing required ({string.Join(", ", missingRequired)})"));
        }

        return new ValidationResult(errors.Count == 0, [.. errors]);
    }

    private static HashSet<string> OptionalKeys(IReadOnlyDictionary<string, List<string?>> comments)
    {
        HashSet<string> keys = [];
        foreach ((string key, List<string?> values) in comments)
        {
            if (values.Exists(comment => !string.IsNullOrEmpty(comment) && OptionalCommentRegex().IsMatch(comment)))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    [GeneratedRegex(@"\boptional\b", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
    private static partial Regex OptionalCommentRegex();
}
