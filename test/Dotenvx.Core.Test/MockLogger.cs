using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Test;

public class MockLogger : IDotenvxLogger
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

    public DotenvxLogLevel? SetLevelCalled { get; private set; }

    public Action<string>? BeforeLog { get; set; }

    public void Error(string message)
    {
        BeforeLog?.Invoke(message);
        _errorMessages.Add(message);
    }

    public void Warn(string message)
    {
        BeforeLog?.Invoke(message);
        _warningMessages.Add(message);
    }

    public void Success(string message)
    {
        BeforeLog?.Invoke(message);
        _infoMessages.Add(message);
    }

    public void Info(string message)
    {
        BeforeLog?.Invoke(message);
        _infoMessages.Add(message);
    }

    public void Help(string message)
    {
        BeforeLog?.Invoke(message);
        _infoMessages.Add(message);
    }

    public void Verbose(string message)
    {
        BeforeLog?.Invoke(message);
        _verboseMessages.Add(message);
    }

    public void Debug(string message)
    {
        BeforeLog?.Invoke(message);
        _debugMessages.Add(message);
    }

    public void SetLevel(DotenvxLogLevel level)
    {
        SetLevelCalled = level;
    }
}