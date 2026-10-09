using System.Text;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public record RemoveEnvKeyResult(bool Changed, string Key, string Filepath);

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/readEnvKey.js and removeEnvKey.js (the `.env.keys` editing helpers used by
/// the `native` services; `upsertEnvKey.js` is <see cref="UpsertEnvKeyHelper" />).
/// </remarks>
public static partial class EnvKeyFiles
{
    [GeneratedRegex(@"^\s*(?:export\s+)?[A-Za-z_][A-Za-z0-9_]*\s*=")]
    private static partial Regex AnyEnvKeyLine();

    private static bool Ignored(DotenvxException error, string[]? ignore)
    {
        return ignore?.Contains(error.Code) == true;
    }

    /// <summary>
    /// readEnvKey.js: the last value of <paramref name="key" /> in <paramref name="filepath" />. A missing file or key
    /// is a MISSING_ENV_FILE / MISSING_KEY error that is thrown only when <paramref name="strict" /> and not listed in
    /// <paramref name="ignore" />, otherwise the result is null.
    /// </summary>
    public static string? ReadEnvKey(string key, string filepath, bool strict = false, string[]? ignore = null)
    {
        byte[] bytes;
        try
        {
            bytes = Fsx.ReadFileSync(filepath);
        }
        catch (Exception)
        {
            var error = new MissingEnvFileException(filepath);
            if (Ignored(error, ignore))
            {
                return null;
            }

            return strict ? throw error : null;
        }

        ScanResult scanned = Primitives.Primitives.Scan(Encoding.UTF8.GetString(bytes), new ScanOptions());
        string? value = scanned.Parsed.TryGetValue(key, out List<string>? values) ? values[^1] : null;
        if (value is null)
        {
            var error = new MissingKeyException(key);
            if (Ignored(error, ignore))
            {
                return null;
            }

            if (strict)
            {
                throw error;
            }
        }

        return value;
    }

    /// <summary>removeEnvKey.js: drop <paramref name="key" /> from the keys file, deleting the file when no keys remain.</summary>
    public static RemoveEnvKeyResult RemoveEnvKey(string key, string keysFilepath = ".env.keys")
    {
        string resolved = NodePath.Resolve(keysFilepath);
        if (!Fsx.ExistsSync(resolved))
        {
            return new RemoveEnvKeyResult(false, key, keysFilepath);
        }

        string src = Encoding.UTF8.GetString(Fsx.ReadFileSync(resolved));
        string eol = src.Contains("\r\n") ? "\r\n" : "\n";
        var keyPattern = new Regex($@"^\s*(?:export\s+)?{Regex.Escape(key)}\s*=");
        List<string> lines = src.Length > 0 ? [.. Regex.Split(src, "\r?\n")] : [];

        while (lines.Count > 0 && lines[^1] == "")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        List<string> nextLines = [.. lines.Where(line => !keyPattern.IsMatch(line))];
        bool changed = nextLines.Count != lines.Count;

        if (changed)
        {
            if (nextLines.Any(line => AnyEnvKeyLine().IsMatch(line)))
            {
                Fsx.WriteFileSync(resolved, string.Join(eol, nextLines) + eol);
            }
            else
            {
                File.Delete(resolved);
            }
        }

        return new RemoveEnvKeyResult(changed, key, keysFilepath);
    }
}
