namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static partial class Helpers
{
    internal static string ResolveHome(string path)
    {
        return path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;
    }
}