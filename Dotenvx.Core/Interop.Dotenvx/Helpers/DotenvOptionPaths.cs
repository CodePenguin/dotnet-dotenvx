using Dotenvx.Core.Interop.Dotenvx.Types;

namespace Dotenvx.Core.Interop.Dotenvx.Helpers
{
    public static class DotenvOptionPaths
    {
        public static List<string> Execute(ConfigOptions options)
        {
            // TODO: Translate logic from dotenvOptionPaths.js
            return options.Path ?? new List<string>();
        }
    }
}
