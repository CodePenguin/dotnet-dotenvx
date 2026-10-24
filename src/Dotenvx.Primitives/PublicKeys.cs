namespace Dotenvx.Primitives;

/// <remarks>
/// Counterpart of the @dotenvx/primitives `publickeys` export: the last value of every key whose name
/// starts with DOTENV_PUBLIC_KEY, in first-appearance order. Checked against the live package by
/// PrimitivesOracleParityTest.
/// </remarks>
public static partial class Primitives
{
    public static string[] PublicKeys(string src)
    {
        Dictionary<string, string> last = [];
        foreach (ScanEntry entry in Scan(src))
        {
            if (entry.Key.StartsWith("DOTENV_PUBLIC_KEY", StringComparison.Ordinal))
            {
                last[entry.Key] = entry.Value;
            }
        }

        return [.. last.Values];
    }
}
