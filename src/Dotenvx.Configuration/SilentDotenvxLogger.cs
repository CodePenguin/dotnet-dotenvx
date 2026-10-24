using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Configuration;

// A configuration provider must not write to the console unless asked: it replaces the default console logger.
internal sealed class SilentDotenvxLogger : IDotenvxLogger
{
    public void Error(string message) { }
    public void Warn(string message) { }
    public void Success(string message) { }
    public void Info(string message) { }
    public void Help(string message) { }
    public void Verbose(string message) { }
    public void Debug(string message) { }
    public void SetLevel(DotenvxLogLevel level) { }
}
