using Dotenvx.Interop.IgnoreJs;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/isIgnoringDotenvKeys.js</remarks>
public static partial class Helpers
{
    /// <summary>
    ///     .gitignore equivalent-ish check like the NodeJS ignore package.
    /// </summary>
    public static bool IsIgnoringDotenvKeys()
    {
        if (!Fsx.ExistsSync(".gitignore"))
        {
            return false;
        }

        string gitignore = Fsx.ReadFileX(".gitignore");
        Ignore ig = new Ignore().Add(gitignore);

        return ig.Ignores(".env.keys");
    }
}