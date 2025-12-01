using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Logging;

public class DotenvxLoggerOptions
{
    public DotenvxLogLevel LogLevel { get; set; } = DotenvxLogLevel.Info;
}