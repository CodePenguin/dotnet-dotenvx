using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// No `treeify.js` exists in this submodule at this pin (confirmed via find - a stale reference).
/// `ls.js` actually renders via `@dotenvx/tooling`'s live-callable `objectTreeify` export directly
/// (`const { objectTreeify: treeify } = require('@dotenvx/tooling')`), not a local dotenvx helper.
/// Cross-checked 2026-10-02 against that real export with the exact empty-object-leaf tree shape
/// `ArrayToTree` actually produces (not `null` leaves) - output format (`├─`/`└─`/`│  ` characters,
/// trailing-newline trimming, single-item and empty-tree cases) matches exactly; no bug found here,
/// unlike `Interop.PicomatchJs`/`Interop.IgnoreJs` (see traps #59/#60), which both had real drift
/// from their own live-callable real counterparts.
/// </remarks>
public static partial class Helpers
{
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