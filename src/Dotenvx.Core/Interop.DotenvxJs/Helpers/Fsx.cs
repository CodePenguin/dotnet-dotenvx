using System.Linq;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public delegate string ReadFileXDelegate(string filepath, string? encoding = null);

/// <remarks>Reference source: dotenvx/src/lib/helpers/fsx.js</remarks>
public static class Fsx
{
    private const string Encoding = "utf8";

    // Node's fs.writeFileSync/appendFileSync never emit a BOM for utf8; System.Text.Encoding.UTF8 does.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static Action<string, string> AppendFileSync { get; internal set; } = DefaultAppendFileSync;
    public static Func<string, bool> ExistsSync { get; internal set; } = DefaultExistsSync;
    public static Func<string, string[]> ReadDirSync { get; internal set; } = DefaultReadDirSync;
    public static ReadFileXDelegate ReadFileX { get; internal set; } = DefaultReadFileX;
    public static Func<string, byte[]> ReadFileSync { get; internal set; } = DefaultReadFileSync;
    public static Action<string, string> WriteFileSync { get; internal set; } = DefaultWriteFileSync;
    public static Action<string, string> WriteFileX { get; internal set; } = DefaultWriteFileX;

    private static void DefaultAppendFileSync(string path, string data)
    {
        File.AppendAllText(path, data, Utf8NoBom);
    }

    private static bool DefaultExistsSync(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    private static string[] DefaultReadDirSync(string path)
    {
        // Node's fs.readdirSync returns bare entry names, not full paths - Directory
        // .GetFileSystemEntries returns full paths, which would silently break any caller
        // matching against a bare filename (e.g. FindEnvFiles's `file.StartsWith(".env")`).
        // Unused anywhere before this fix (confirmed via grep), so this was latent, not live.
        return Directory.GetFileSystemEntries(path).Select(Path.GetFileName).ToArray()!;
    }

    private static string DefaultReadFileX(string filepath, string? encoding = null)
    {
        if (string.IsNullOrEmpty(encoding))
        {
            encoding = Encoding;
        }

        Encoding enc = encoding.ToLower() switch
        {
            "ascii" => System.Text.Encoding.ASCII,
            "utf8" => System.Text.Encoding.UTF8,
            "utf16" => System.Text.Encoding.Unicode,
            "utf32" => System.Text.Encoding.UTF32,
            _ => System.Text.Encoding.GetEncoding(encoding)
        };

        return File.ReadAllText(filepath, enc);
    }

    private static byte[] DefaultReadFileSync(string path)
    {
        return File.ReadAllBytes(path);
    }

    private static void DefaultWriteFileSync(string path, string data)
    {
        File.WriteAllText(path, data);
    }

    private static void DefaultWriteFileX(string filepath, string str)
    {
        File.WriteAllText(filepath, str, Utf8NoBom);
    }

    // Method to reset to defaults for tests
    internal static void ResetToDefaults()
    {
        AppendFileSync = DefaultAppendFileSync;
        ExistsSync = DefaultExistsSync;
        ReadDirSync = DefaultReadDirSync;
        ReadFileX = DefaultReadFileX;
        ReadFileSync = DefaultReadFileSync;
        WriteFileSync = DefaultWriteFileSync;
        WriteFileX = DefaultWriteFileX;
    }
}