namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/armoredKeyDisplay.js</remarks>
internal static class ArmoredKeyDisplay
{
    public static string Display(string? publicKey)
    {
        if (string.IsNullOrEmpty(publicKey))
        {
            return "";
        }

        string prefix = (publicKey.Length > 6 ? publicKey[..6] : publicKey).ToUpperInvariant();

        return prefix.Length <= 3 ? prefix : $"{prefix[..3]} {prefix[3..]}";
    }
}
