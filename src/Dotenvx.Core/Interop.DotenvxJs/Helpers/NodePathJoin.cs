namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No single upstream reference file - mirrors Node's `path.join(directory, file)` semantics
/// specifically for prebuild.js/precommit.js's `path.join(this.directory, _file)` call, which
/// .NET's Path.Combine does NOT replicate: Path.Combine(".", "x") keeps the leading "./"/".\"
/// (Node's path.join collapses it away entirely), and Path.Combine doesn't normalize forward
/// slashes already present in its arguments to the platform separator the way Node's path.join
/// does (e.g. Path.Combine("apps/backend", ".env") on Windows gives "apps/backend\.env" - mixed
/// separators - while Node gives "apps\backend\.env" throughout). Both behaviors are confirmed
/// live via `node -e "console.log(path.join(...))"` against the exact directory/file shapes
/// prebuild/precommit actually pass (a bare "." default, or a simple relative subdirectory).
/// This matters because prebuild.js/precommit.js compare the joined path with FULL STRING
/// EQUALITY against literal exempt filenames and embed it verbatim in thrown/warned messages -
/// a mismatched separator style would silently break the exemption check and produce
/// different-looking messages whenever `directory` is left at its default ".".
/// </remarks>
public static partial class Helpers
{
    public static string NodePathJoin(string directory, string file)
    {
        string normalizedFile = Normalize(file);
        string trimmedDirectory = Normalize(directory).TrimEnd(Path.DirectorySeparatorChar);

        // "." and "./" (Node's most common directory default) both collapse away entirely.
        return trimmedDirectory is "." or ""
            ? normalizedFile
            : $"{trimmedDirectory}{Path.DirectorySeparatorChar}{normalizedFile}";

        static string Normalize(string s)
        {
            return s.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}
