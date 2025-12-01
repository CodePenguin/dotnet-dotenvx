using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Shared;

public interface IDotenvxLogger
{
    void Error(string message);
    void Warn(string message);
    void Success(string message);
    void SuccessV(string message);
    void Info(string message);
    void Help(string message);
    void Verbose(string message);
    void Debug(string message);
    void SetLevel(DotenvxLogLevel level);
}