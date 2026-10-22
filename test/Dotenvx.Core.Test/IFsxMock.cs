namespace Dotenvx.Core.Test;

internal interface IFsxMock
{
    byte[] ReadFileSync(string filepath);
    string ReadFileX(string filepath, string? encoding = null);
}
