namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/detectEncoding.js</remarks>
internal static partial class Helpers
{
    public static string DetectEncoding(string filepath)
    {
        byte[] buffer = Fsx.ReadFileSync(filepath);

        return buffer.Length switch
        {
            // check for UTF-16LE BOM (Byte Order Mark)
            >= 2 when buffer[0] == 0xFF && buffer[1] == 0xFE => "utf16le",
            // check for UTF-8 BOM
            >= 3 when buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF => "utf8",
            _ => "utf8"
        };
    }
}
