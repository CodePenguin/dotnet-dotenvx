using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx;

/// <remarks>
/// Reference source: dotenvx/src/shared/logger.js. The logger the library uses when no <see cref="ConfigOptions.Logger" /> is
/// given (<see cref="Dotenvx.Logger" />, the `logger` export of main.js): colored messages on stderr, `info` on stdout, and
/// only those at or below the current level (`info` until something changes it).
/// </remarks>
public sealed class DotenvxConsoleLogger : IDotenvxLogger
{
    private readonly object _gate = new();

    /// <summary>The current level (`logger.level`).</summary>
    public DotenvxLogLevel Level { get; private set; } = DotenvxLogLevel.Info;

    public void Error(string message)
    {
        Write(DotenvxLogLevel.Error, ColorStream.StdErr,
            Shared.Bold(Shared.GetColor("red", ColorStream.StdErr)($"☠ {message}"), ColorStream.StdErr));
    }

    public void InfoError(string message)
    {
        Write(DotenvxLogLevel.Error, ColorStream.StdErr, Shared.GetColor("gray", ColorStream.StdErr)(message));
    }

    public void Warn(string message)
    {
        Write(DotenvxLogLevel.Warn, ColorStream.StdErr, Shared.GetColor("orangered", ColorStream.StdErr)($"⚠ {message}"));
    }

    public void Success(string message)
    {
        Write(DotenvxLogLevel.Info, ColorStream.StdErr, Shared.GetColor("amber", ColorStream.StdErr)(message));
    }

    public void Info(string message)
    {
        Write(DotenvxLogLevel.Info, ColorStream.StdOut, Shared.GetColor("gray", ColorStream.StdOut)(message));
    }

    public void Help(string message)
    {
        Write(DotenvxLogLevel.Info, ColorStream.StdErr, Shared.GetColor("dodgerblue", ColorStream.StdErr)(message));
    }

    public void Verbose(string message)
    {
        Write(DotenvxLogLevel.Verbose, ColorStream.StdErr, Shared.GetColor("plum", ColorStream.StdErr)($"┆ {message}"));
    }

    public void Debug(string message)
    {
        Write(DotenvxLogLevel.Debug, ColorStream.StdErr, Shared.GetColor("plum", ColorStream.StdErr)($"┆ {message}"));
    }

    public void SetLevel(DotenvxLogLevel level)
    {
        lock (_gate)
        {
            Level = level;
        }
    }

    private void Write(DotenvxLogLevel level, ColorStream stream, string formatted)
    {
        lock (_gate)
        {
            if (level > Level)
            {
                return;
            }

            (stream == ColorStream.StdErr ? Console.Error : Console.Out).Write(formatted + "\n"); // console.log ends a line with LF on every OS
        }
    }
}
