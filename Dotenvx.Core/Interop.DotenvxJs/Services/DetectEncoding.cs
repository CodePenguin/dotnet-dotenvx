using System.Text;

namespace Dotenvx.Core.Interop.DotenvxJs.Services;

public static class DetectEncoding
{
    public static Encoding Execute(string filepath)
    {
        // TODO: Translate logic from detectEncoding.js
        return Encoding.UTF8;
    }
}