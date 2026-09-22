namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/canonicalEnvFilename.js</remarks>
public static partial class Helpers
{
    public static string CanonicalEnvFilename(string filepath)
    {
        string filename = Path.GetFileName(filepath).ToLower();

        if (filename.StartsWith(".env") && filename.EndsWith(".txt"))
        {
            filename = filename[..^4];
        }

        return filename;
    }
}
