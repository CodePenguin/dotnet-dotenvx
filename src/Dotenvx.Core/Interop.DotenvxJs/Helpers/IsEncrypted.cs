namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: @dotenvx/primitives' `encrypted(value)` export (no readable source exists -
/// isEncrypted.js, this file's old reference, was deleted from dotenvx itself well before the
/// v2.24.1 pin once its logic moved there). Verified via live-oracle decompile: a falsy-check then
/// prefix-check, matching `value?.StartsWith(Prefix) == true` exactly for every realistic input.
/// </remarks>
public static partial class Helpers
{
    public static bool IsEncrypted(string? value)
    {
        return value?.StartsWith(Prefix) == true;
    }
}