namespace Dotenvx.Interop.DotenvxJs.Events;

/// <remarks>
/// Reference source: the `{ exitCode, error, errorCount, signal }` result object the upstream CLI actions return to
/// lib/events/cli.js. Actions here return an exit code only, so they report the rest to the running command's scope.
/// </remarks>
public sealed class EventScope
{
    private static readonly AsyncLocal<EventScope?> Scope = new();

    public Exception? Error { get; private set; }

    public string? ErrorCode { get; private set; }

    public int? ErrorCount { get; private set; }

    public string? Signal { get; private set; }

    public static EventScope? Current => Scope.Value;

    public static EventScope Begin()
    {
        return Scope.Value = new EventScope();
    }

    public static void End()
    {
        Scope.Value = null;
    }

    /// <summary>`return { exitCode, error }`</summary>
    public static void Fail(Exception error)
    {
        if (Scope.Value is { } scope)
        {
            scope.Error = error;
        }
    }

    /// <summary>`return { error: { code } }` (executeCommand's `COMMAND_NOT_FOUND`)</summary>
    public static void Fail(string code)
    {
        if (Scope.Value is { } scope)
        {
            scope.ErrorCode = code;
        }
    }

    /// <summary>`return { exitCode, errorCount }`</summary>
    public static void Count(int errorCount)
    {
        if (Scope.Value is { } scope)
        {
            scope.ErrorCount = errorCount;
        }
    }

    /// <summary>`return { exitCode, signal }`</summary>
    public static void SignalReceived(string? signal)
    {
        if (Scope.Value is { } scope && !string.IsNullOrEmpty(signal))
        {
            scope.Signal = signal;
        }
    }
}
