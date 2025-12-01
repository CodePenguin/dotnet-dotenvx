using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Logging;

namespace Dotenvx.Logging;

public class DotenvxLogger(ILogger<DotenvxLogger> logger, DotenvxLoggerOptions options) : IDotenvxLogger
{
    private static readonly string Version = typeof(DotenvxLogger).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    public void Error(string message)
    {
        logger.LogError("{Message}", Shared.Bold(Shared.GetColor("red")(message)));
    }

    public void Warn(string message)
    {
        logger.LogWarning("{Message}", Shared.GetColor("orangered")(message));
    }

    public void Success(string message)
    {
        logger.LogInformation("{Message}", Shared.GetColor("green")(message));
    }

    public void SuccessV(string message)
    {
        string formatted = $"[dotenvx@{Version}] {message}";
        logger.LogInformation("{Message}", Shared.GetColor("olive")(formatted));
    }

    public void Info(string message)
    {
        logger.LogInformation("{Message}", message);
    }

    public void Help(string message)
    {
        logger.LogInformation("{Message}", Shared.GetColor("dodgerblue")(message));
    }

    public void Verbose(string message)
    {
        if (options.LogLevel < DotenvxLogLevel.Verbose)
        {
            return;
        }

        logger.LogDebug("{Message}", Shared.GetColor("plum")(message));
    }

    public void Debug(string message)
    {
        if (options.LogLevel < DotenvxLogLevel.Debug)
        {
            return;
        }

        logger.LogDebug("{Message}", Shared.GetColor("plum")(message));
    }

    public void SetLevel(DotenvxLogLevel level)
    {
        options.LogLevel = level;
    }
}