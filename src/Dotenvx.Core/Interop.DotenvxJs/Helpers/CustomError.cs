using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// errors.js `custom()` applied to an `{ code, message }` error returned by primitives' `parse()`: keeps the
/// code and the (already prefixed) message; help is the generic `fix: [issue url]` for the code. Only
/// DECRYPTION_FAILED is ever returned by primitives, and it maps onto the typed exception.
/// </remarks>
internal static partial class Helpers
{
    public static Exception CustomError(PrimitivesError error)
    {
        const string prefix = "[DECRYPTION_FAILED] ";
        if (error.Code == "DECRYPTION_FAILED")
        {
            return new DecryptionFailedException(error.Message.StartsWith(prefix)
                ? error.Message[prefix.Length..]
                : error.Message);
        }

        return new CustomException(error.Message, code: error.Code);
    }
}
