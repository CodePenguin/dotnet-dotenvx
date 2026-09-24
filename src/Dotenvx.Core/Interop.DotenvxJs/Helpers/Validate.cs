using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/validate.js</remarks>
public static partial class Helpers
{
    public static bool ValidateExample(Dictionary<string, string[]> example, IReadOnlyDictionary<string, string> env,
        Dictionary<string, string?[]> comments, out string[] missingRequired)
    {
        HashSet<string> optional = OptionalKeys(comments);
        List<string> missing = [];

        foreach (string key in example.Keys)
        {
            bool hasValue = env.TryGetValue(key, out string? value);
            bool isMissing = !hasValue || value!.Trim().Length == 0;
            if (!optional.Contains(key) && isMissing)
            {
                missing.Add(key);
            }
        }

        missingRequired = [.. missing];
        return missingRequired.Length == 0;
    }

    private static HashSet<string> OptionalKeys(Dictionary<string, string?[]> comments)
    {
        HashSet<string> keys = [];
        foreach ((string key, string?[] values) in comments)
        {
            if (Array.Exists(values, comment => comment != null && OptionalCommentRegex().IsMatch(comment)))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    [GeneratedRegex(@"\boptional\b", RegexOptions.IgnoreCase)]
    private static partial Regex OptionalCommentRegex();
}
