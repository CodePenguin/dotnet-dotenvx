using Microsoft.Extensions.Logging;

namespace Dotenvx.Logging;

/// <remarks>
/// Reference source: dotenvx/src/shared/logger.js. Carries which of dotenvx's named log levels a
/// message came from through Microsoft.Extensions.Logging's EventId, since several of them
/// (error/infoerror/warn/success/help/verbose/debug) collapse onto the same Microsoft.Extensions
/// LogLevel but must still be routed to different physical streams by DotenvxCustomLogger - only
/// "info" goes to stdout upstream; everything else (including "success") goes to stderr.
/// </remarks>
public static class DotenvxLogEvents
{
    public static readonly EventId Error = new(1, nameof(Error));
    public static readonly EventId InfoError = new(2, nameof(InfoError));
    public static readonly EventId Warn = new(3, nameof(Warn));
    public static readonly EventId Success = new(4, nameof(Success));
    public static readonly EventId Info = new(5, nameof(Info));
    public static readonly EventId Help = new(6, nameof(Help));
    public static readonly EventId Verbose = new(7, nameof(Verbose));
    public static readonly EventId Debug = new(8, nameof(Debug));
}
