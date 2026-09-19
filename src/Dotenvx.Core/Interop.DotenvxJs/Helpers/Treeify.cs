using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/treeify.js</remarks>
public static partial class Helpers
{
    /// <summary>
    ///     Approximate equivalent functionality of Node.js object-treeify package.
    /// </summary>
    public static string Treeify(Dictionary<string, object> tree, string prefix = "")
    {
        // Only the outermost call should strip the trailing newline - recursive calls append
        // their subtree directly into the parent's StringBuilder and must keep it.
        bool isRoot = prefix.Length == 0;
        string result = TreeifyInner(tree, prefix);
        return isRoot ? result.TrimEnd('\n') : result;
    }

    private static string TreeifyInner(Dictionary<string, object> tree, string prefix)
    {
        StringBuilder sb = new();

        List<KeyValuePair<string, object>> items = tree.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            bool last = i == items.Count - 1;
            string key = items[i].Key;
            object value = items[i].Value;

            sb.Append(prefix);
            sb.Append(last ? "└─ " : "├─ ");
            sb.Append(key);
            sb.Append('\n');

            if (value is not Dictionary<string, object> subTree)
            {
                continue;
            }

            string newPrefix = prefix + (last ? "   " : "│  ");
            sb.Append(TreeifyInner(subTree, newPrefix));
        }

        return sb.ToString();
    }
}