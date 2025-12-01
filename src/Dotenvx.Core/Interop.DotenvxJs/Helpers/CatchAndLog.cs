using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/catchAndLog.js</remarks>
public static partial class Helpers
{
    public static void CatchAndLog(IDotenvxLogger logger, Exception error)
    {
        logger.Error(error.Message);
        if (error is not DotenvxException ex)
        {
            return;
        }

        if (!string.IsNullOrEmpty(ex.Help))
        {
            logger.Help(ex.Help);
        }

        logger.Debug($"ERROR_CODE: {ex.Code}");
    }
}