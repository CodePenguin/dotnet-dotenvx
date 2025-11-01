namespace Dotenvx.Core.Interop.DotenvxJs.Shared;

public interface IDotenvxLogger
{
    void Error(string message);
    void Warn(string message);
    void Success(string msg);
    void SuccessV(string msg);
    void Info(string message);
    void Help(string msg);
    void Verbose(string msg);
    void Debug(string message);
}