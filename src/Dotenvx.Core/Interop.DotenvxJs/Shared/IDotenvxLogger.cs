using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx;

public interface IDotenvxLogger
{
    void Error(string message);

    /// <summary>`logger.infoerror`: a gray message on stderr at the error level.</summary>
    void InfoError(string message)
    {
        Info(message);
    }
    void Warn(string message);
    void Success(string message);
    void Info(string message);
    void Help(string message);
    void Verbose(string message);
    void Debug(string message);
    void SetLevel(DotenvxLogLevel level);
}