namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/resolveDirectoryFilepath.js</remarks>
internal static partial class Helpers
{
    public static string ResolveDirectoryFilepath(string filepath, string filename)
    {
        if (Fsx.IsDirectorySync(filepath))
        {
            return NodePathJoin(filepath, filename);
        }

        return filepath;
    }
}
