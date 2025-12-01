using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/append.js</remarks>
public static partial class Helpers
{
    public static string Append(string src, string key, string appendValue)
    {
        Dictionary<string, object> parsed = DotenvParse(src, true, true);
        if (parsed.TryGetValue(key, out object? value))
        {
            string originalValue = (string)value;
            string newValue = $"{originalValue},{appendValue}";
            // Simple replace
            string pattern = $@"^{key}\s*=\s*[""']?{Regex.Escape(originalValue)}[""']?$";
            return Regex.Replace(src, pattern, $"{key}=\"{newValue}\"", RegexOptions.Multiline);
        }

        string newPart = $"{key}=\"{appendValue}\"";
        if (src.EndsWith('\n'))
        {
            newPart += '\n';
        }
        else
        {
            newPart = '\n' + newPart;
        }

        return src + newPart;
    }
}