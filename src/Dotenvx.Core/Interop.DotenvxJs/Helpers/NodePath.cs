namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Mirrors Node's `path.resolve`, which .NET's Path.GetFullPath differs from in one visible way: the result never ends in a
/// separator (`path.resolve('.env/')` is `.../.env`, so `-f .env/` reads the file `.env`), where GetFullPath keeps the trailing
/// separator and the read then fails. A root (`C:\` or `/`) keeps its separator, like Node.
/// </remarks>
public static class NodePath
{
    public static string Resolve(string path)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full) ?? "";
        if (full.Length <= root.Length)
        {
            return full;
        }

        string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return trimmed.Length < root.Length ? root : trimmed;
    }

    /// <summary>Node's `path.basename`: the last segment, ignoring trailing separators (`.env/` is `.env`).</summary>
    public static string Basename(string path)
    {
        char[] separators = OperatingSystem.IsWindows() ? ['/', '\\'] : ['/'];
        string trimmed = path.TrimEnd(separators);
        int last = trimmed.LastIndexOfAny(separators);
        string name = last < 0 ? trimmed : trimmed[(last + 1)..];

        // a drive letter alone ("C:") has no basename on Windows
        return OperatingSystem.IsWindows() && last < 0 && name.Length == 2 && name[1] == ':' ? "" : name;
    }
}
