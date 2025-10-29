using dotnet_dotenvx.Actions;

using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx.Test;

public class MockLogger : ILogger<RunAction>
{
    private readonly List<string> _debugMessages = [];
    private readonly List<string> _errorMessages = [];
    private readonly List<string> _infoMessages = [];
    private readonly List<string> _verboseMessages = [];
    private readonly List<string> _warningMessages = [];

    public IReadOnlyList<string> DebugMessages => _debugMessages;
    public IReadOnlyList<string> ErrorMessages => _errorMessages;
    public IReadOnlyList<string> InfoMessages => _infoMessages;
    public IReadOnlyList<string> VerboseMessages => _verboseMessages;
    public IReadOnlyList<string> WarningMessages => _warningMessages;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return null!;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        switch (logLevel)
        {
            case LogLevel.Debug:
                _debugMessages.Add(message);
                break;
            case LogLevel.Critical:
            case LogLevel.Error:
                _errorMessages.Add(message);
                break;
            case LogLevel.Information:
                _infoMessages.Add(message);
                break;
            case LogLevel.Trace:
                _verboseMessages.Add(message);
                break;
            case LogLevel.Warning:
                _warningMessages.Add(message);
                break;
            case LogLevel.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel));
        }
    }
}