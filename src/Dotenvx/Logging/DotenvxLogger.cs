using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Logging;

namespace Dotenvx.Logging;

/// <remarks>
/// Reference source: dotenvx/src/shared/logger.js. Fixed 2026-09-25: every level except
/// success/info/help was missing its symbol prefix entirely (error's "☠ ", warn's "⚠ ",
/// verbose/debug's "┆ ") - `logger.js`'s own `error`/`warn`/`verbose`/`debug` formatter functions
/// add these themselves, they aren't something each call site includes, so every single message at
/// those levels was missing its prefix across the whole CLI. `Info` was also missing its "gray"
/// coloring entirely. `Success` used "green" instead of upstream's actual "amber". `SuccessV` (a
/// fabricated concept with no upstream equivalent - real logger.js has exactly one `success`
/// function, no distinct "verbose success"/version-tagged variant) has been removed; its two
/// former callers (RunAction.cs, Main.cs's Config()) now call Success directly with the message
/// upstream's own call sites build (`⟐ injected env (N)...` - not the invented
/// "[dotenvx@version] injecting env (N)..." text this port had instead). All of this went
/// undetected because the parity test suite deliberately never diffs stderr content (see
/// docs/porting-traps.md, parity-suite "traps" list), and visual comparisons of rendered text
/// missed it because only a raw byte diff (`xxd` against the real Node CLI) shows color codes and prefixes (docs/porting-traps.md #24).
/// </remarks>
public class DotenvxLogger(ILogger<DotenvxLogger> logger, DotenvxLoggerOptions options) : IDotenvxLogger
{
    public void Error(string message)
    {
        logger.LogError(DotenvxLogEvents.Error, "{Message}",
            Shared.Bold(Shared.GetColor("red", ColorStream.StdErr)($"☠ {message}"), ColorStream.StdErr));
    }

    public void Warn(string message)
    {
        logger.LogWarning(DotenvxLogEvents.Warn, "{Message}",
            Shared.GetColor("orangered", ColorStream.StdErr)($"⚠ {message}"));
    }

    public void Success(string message)
    {
        logger.LogInformation(DotenvxLogEvents.Success, "{Message}",
            Shared.GetColor("amber", ColorStream.StdErr)(message));
    }

    public void Info(string message)
    {
        logger.LogInformation(DotenvxLogEvents.Info, "{Message}",
            Shared.GetColor("gray", ColorStream.StdOut)(message));
    }

    public void Help(string message)
    {
        logger.LogInformation(DotenvxLogEvents.Help, "{Message}",
            Shared.GetColor("dodgerblue", ColorStream.StdErr)(message));
    }

    public void Verbose(string message)
    {
        if (options.LogLevel < DotenvxLogLevel.Verbose)
        {
            return;
        }

        logger.LogDebug(DotenvxLogEvents.Verbose, "{Message}",
            Shared.GetColor("plum", ColorStream.StdErr)($"┆ {message}"));
    }

    public void Debug(string message)
    {
        if (options.LogLevel < DotenvxLogLevel.Debug)
        {
            return;
        }

        logger.LogDebug(DotenvxLogEvents.Debug, "{Message}",
            Shared.GetColor("plum", ColorStream.StdErr)($"┆ {message}"));
    }

    public void SetLevel(DotenvxLogLevel level)
    {
        options.LogLevel = level;
    }
}
