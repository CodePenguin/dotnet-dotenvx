namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/resolveEnvKeysFile.js: each `-fk` value that is a directory means
/// `&lt;directory&gt;/.env.keys`; missing paths are kept so later error handling can report them. No `-fk` stays null.
/// </remarks>
internal static partial class Helpers
{
    public static string[]? ResolveEnvKeysFile(string[]? envKeysFile)
    {
        return envKeysFile?.Select(filepath => ResolveDirectoryFilepath(filepath, ".env.keys")).ToArray();
    }
}
