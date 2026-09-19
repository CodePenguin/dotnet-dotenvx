using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Logging;

namespace Dotenvx.Logging;

public class DotenvxLogger(ILogger<DotenvxLogger> logger, DotenvxLoggerOptions options) : IDotenvxLogger
{
    private static readonly string Version = typeof(DotenvxLogger).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public void Error(string message)
    {
        logger.LogError(DotenvxLogEvents.Error, "{Message}", Shared.Bold(Shared.GetColor("red")(message)));
    }

    public void Warn(string message)
    {
        logger.LogWarning(DotenvxLogEvents.Warn, "{Message}", Shared.GetColor("orangered")(message));
    }

    public void Success(string message)
    {
        logger.LogInformation(DotenvxLogEvents.Success, "{Message}", Shared.GetColor("green")(message));
    }

    public void SuccessV(string message)
    {
        string formatted = $"[dotenvx@{Version}] {message}";
        logger.LogInformation(DotenvxLogEvents.Success, "{Message}", Shared.GetColor("olive")(formatted));
    }

    public void Info(string message)
    {
        logger.LogInformation(DotenvxLogEvents.Info, "{Message}", message);
    }

    public void Help(string message)
    {
        logger.LogInformation(DotenvxLogEvents.Help, "{Message}", Shared.GetColor("dodgerblue")(message));
    }

    public void Verbose(string message)
    {
        if (options.LogLevel < DotenvxLogLevel.Verbose)
        {
            return;
        }

        logger.LogDebug(DotenvxLogEvents.Verbose, "{Message}", Shared.GetColor("plum")(message));
    }

    public void Debug(string message)
    {
        if (options.LogLevel < DotenvxLogLevel.Debug)
        {
            return;
        }

        logger.LogDebug(DotenvxLogEvents.Debug, "{Message}", Shared.GetColor("plum")(message));
    }

    public void SetLevel(DotenvxLogLevel level)
    {
        options.LogLevel = level;
    }
}