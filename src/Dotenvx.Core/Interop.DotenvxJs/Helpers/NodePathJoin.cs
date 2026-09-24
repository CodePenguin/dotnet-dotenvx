namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No single upstream reference file - mirrors Node's `path.join(directory, file)` semantics
/// specifically for prebuild.js/precommit.js's `path.join(this.directory, _file)` call and
/// buildCommandEnvs.js's `path.join(filepath, filename)` (via resolveDirectoryFilepath.js), which
/// .NET's Path.Combine does NOT replicate: Path.Combine(".", "x") keeps the leading "./"/".\"
/// (Node's path.join collapses it away entirely), and Path.Combine doesn't normalize forward
/// slashes already present in its arguments to the platform separator the way Node's path.join
/// does (e.g. Path.Combine("apps/backend", ".env") on Windows gives "apps/backend\.env" - mixed
/// separators - while Node gives "apps\backend\.env" throughout).
///
/// Node's path.join performs full LEXICAL segment normalization (like path.normalize), not just a
/// whole-string "is it exactly '.'" check - confirmed live 2026-09-24 while wiring up
/// buildCommandEnvs.js: `path.join('./config', '.env')` collapses to `config\.env` (no leading
/// ".\"), and `path.join('a/../..', 'x')` resolves the ".." traversal to `..\x`. The original
/// version of this helper only special-cased a directory argument that was exactly "." or "./" -
/// sufficient for prebuild/precommit, which only ever pass a bare "." default or a plain
/// subdirectory name with no embedded "./" or ".." - but buildCommandEnvs's directory argument
/// comes straight from user CLI input (`-f ./config`), which routinely includes exactly the
/// prefix the old version didn't handle. Verified against `node -e "console.log(path.join(...))"`
/// for every branch below (trailing/doubled separators, ".." above an empty relative root, an
/// absolute POSIX-style root, and a Windows drive-letter root) before porting.
/// </remarks>
public static partial class Helpers
{
    public static string NodePathJoin(string directory, string file)
    {
        string normalizedDirectory = NormalizePathSegments(directory);

        // "." (Node's most common directory default, and the collapsed form of "./", "a/..", etc.)
        // disappears entirely when joined with something else.
        return normalizedDirectory is "." or ""
            ? NormalizePathSegments(file)
            : $"{normalizedDirectory}{Path.DirectorySeparatorChar}{NormalizePathSegments(file)}";
    }

    private static string NormalizePathSegments(string path)
    {
        string slashNormalized = path.Replace('/', Path.DirectorySeparatorChar);

        // A drive-letter root (e.g. "C:") is preserved verbatim - segment processing below only
        // ever applies to what follows it.
        string root = "";
        string remainder = slashNormalized;
        if (remainder.Length >= 2 && remainder[1] == ':' && char.IsLetter(remainder[0]))
        {
            root = remainder[..2];
            remainder = remainder[2..];
        }

        bool isAbsolute = remainder.StartsWith(Path.DirectorySeparatorChar);

        List<string> stack = [];
        foreach (string segment in remainder.Split(Path.DirectorySeparatorChar))
        {
            switch (segment)
            {
                case "" or ".":
                    continue;
                case "..":
                    if (stack.Count > 0 && stack[^1] != "..")
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }
                    else if (!isAbsolute)
                    {
                        // Above the relative root - Node keeps a leading ".." (e.g. "..", "../..");
                        // above an absolute root it's simply dropped, same as Node.
                        stack.Add("..");
                    }

                    break;
                default:
                    stack.Add(segment);
                    break;
            }
        }

        string joined = string.Join(Path.DirectorySeparatorChar, stack);

        if (isAbsolute)
        {
            return root + Path.DirectorySeparatorChar + joined;
        }

        if (root.Length > 0)
        {
            return joined.Length > 0 ? root + joined : root;
        }

        return joined.Length > 0 ? joined : ".";
    }
}
