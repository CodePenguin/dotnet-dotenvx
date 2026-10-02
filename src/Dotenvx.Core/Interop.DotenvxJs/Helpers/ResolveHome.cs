namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/resolveHome.js (uses path.join semantics - Path.Combine would discard the home dir for "~/x")</remarks>
public static partial class Helpers
{
    private static string ResolveHome(string path)
    {
        return path.StartsWith('~')
            ? NodePathJoin(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;
    }
}