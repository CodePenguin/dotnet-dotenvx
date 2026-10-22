using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/findEnvFiles.js</remarks>
internal static partial class Helpers
{
    private static readonly string[] ReservedEnvFiles =
        [".env.project", ".env.keys", ".env.me", ".env.x", ".env.example"];

    public static string[] FindEnvFiles(string directory)
    {
        string[] files;
        try
        {
            files = Fsx.ReadDirSync(directory);
        }
        catch (DirectoryNotFoundException)
        {
            throw new MissingDirectoryException(directory);
        }

        return [.. files
            .Where(file => file.StartsWith(".env") && !file.EndsWith(".previous") &&
                           !ReservedEnvFiles.Contains(file))];
    }
}
