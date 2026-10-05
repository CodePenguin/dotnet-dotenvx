using System.Text;
using System.Text.RegularExpressions;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public record UpsertEnvKeyResult(bool Changed, string Key, string Value, bool Created, string Filepath);

/// <remarks>Reference source: dotenvx/src/lib/helpers/upsertEnvKey.js</remarks>
public static class UpsertEnvKeyHelper
{
    public static UpsertEnvKeyResult UpsertEnvKey(string key, string value, string keysFilepath = ".env.keys")
    {
        string resolved = Path.GetFullPath(keysFilepath);
        string keyValueLine = $"{key}={value}";
        bool created = !Fsx.ExistsSync(resolved);

        string src = created ? "" : Encoding.UTF8.GetString(Fsx.ReadFileSync(resolved));

        string eol = src.Contains("\r\n") ? "\r\n" : "\n";
        var keyPattern = new Regex($@"^\s*(?:export\s+)?{Regex.Escape(key)}\s*=");
        List<string> lines = src.Length > 0 ? [.. Regex.Split(src, "\r?\n")] : [];

        while (lines.Count > 0 && lines[^1] == "")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        bool replaced = false;
        var nextLines = new List<string>();

        foreach (string line in lines)
        {
            if (keyPattern.IsMatch(line))
            {
                if (!replaced)
                {
                    nextLines.Add(keyValueLine);
                    replaced = true;
                }

                continue;
            }

            nextLines.Add(line);
        }

        if (!replaced)
        {
            nextLines.Add(keyValueLine);
        }

        string nextSrc = string.Join(eol, nextLines) + eol;
        bool changed = created || nextSrc != src;

        if (changed)
        {
            Fsx.WriteFileSync(resolved, nextSrc);
        }

        return new UpsertEnvKeyResult(changed, key, value, created, keysFilepath);
    }
}
