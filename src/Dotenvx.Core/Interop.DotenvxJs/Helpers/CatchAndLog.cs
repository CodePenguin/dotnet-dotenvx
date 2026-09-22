using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

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

        if (error is DotenvxException ex)
        {
            logger.Debug($"ERROR_CODE: {ex.Code}");
        }
    }
}