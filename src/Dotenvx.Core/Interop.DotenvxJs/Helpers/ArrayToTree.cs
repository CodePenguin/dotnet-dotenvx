namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/arrayToTree.js</remarks>
public static partial class Helpers
{
    public static Dictionary<string, object> ArrayToTree(string[] arr)
    {
        Dictionary<string, object> tree = new();

        foreach (string path in arr)
        {
            string normalizedPath = PathNormalize(path);
            string[] parts = normalizedPath.Split('/');

            Dictionary<string, object> current = tree;

            foreach (string part in parts)
            {
                if (!current.ContainsKey(part))
                {
                    current[part] = new Dictionary<string, object>();
                }

                current = (Dictionary<string, object>)current[part];
            }
        }

        return tree;
    }

    private static string PathNormalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }
}