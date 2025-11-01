using Microsoft.Extensions.Logging;

namespace Dotenvx.Core.Interop.DotenvxJs.Shared;

public class DotenvxLogger(ILogger<DotenvxLogger> logger, DotenvxLoggerConfiguration configuration) : IDotenvxLogger
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
        if (!configuration.Verbose)
        {
            return;
        }

        logger.LogDebug("{Message}", Shared.GetColor("plum")(message));
    }

    public void Debug(string message)
    {
        logger.LogDebug("{Message}", Shared.GetColor("plum")(message));
    }
}