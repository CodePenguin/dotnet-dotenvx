using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/catchAndLog.js</remarks>
public static partial class Helpers
{
    public static void CatchAndLog(IDotenvxLogger logger, Exception error)
    {
        string msg = error is DotenvxException dx ? dx.MessageWithHelp : error.Message;
        if (!string.IsNullOrEmpty(msg))
        {
            logger.Error(msg);
        }

        if (error is DotenvxException { Debug: { Length: > 0 } debug })
        {
            logger.Debug(debug);
        }

        string? code = ErrorCodeOf(error);
        if (!string.IsNullOrEmpty(code))
        {
            logger.Debug($"ERROR_CODE: {code}");
        }
    }

    /// <summary>`error.code` of any error this port raises (dotenvx, provider and native store errors).</summary>
    public static string? ErrorCodeOf(Exception error)
    {
        return error switch
        {
            DotenvxException dx => dx.Code,
            ProviderException provider => provider.Code,
            NativeStoreException native => native.Code,
            _ => null
        };
    }
}
