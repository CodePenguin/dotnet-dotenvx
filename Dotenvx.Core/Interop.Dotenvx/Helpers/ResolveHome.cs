namespace Dotenvx.Core.Interop.Dotenvx.Helpers;

public static class ResolveHome
{
    public static string Execute(string path)
    {
        return path.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..])
            : path;
    }
}