namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/enclavedHint.js. A private key held only in a remote Armor enclave cannot be
/// decrypted locally; the error then points to `dotenvx armor open -f &lt;file&gt;`.
/// </remarks>
internal static class EnclavedHint
{
    public static Exception? Apply(Exception? error, string? envFilepath)
    {
        if (error is not DotenvxException { Code: "ENCLAVED" } enclaved || string.IsNullOrEmpty(envFilepath))
        {
            return error;
        }

        bool plain = envFilepath.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or
            '_' or '.' or '/' or '-');
        enclaved.SetHelp($"hint: [dotenvx armor open -f {(plain ? envFilepath : Helpers.Escape(envFilepath))}]");

        return error;
    }
}
