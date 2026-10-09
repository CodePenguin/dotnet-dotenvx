using System.Linq;
using System.Text;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public delegate string ReadFileXDelegate(string filepath, string? encoding = null);

/// <remarks>Reference source: dotenvx/src/lib/helpers/fsx.js</remarks>
public static class Fsx
{
    private const string Encoding = "utf8";

    // Node's fs.writeFileSync/appendFileSync never emit a BOM for utf8; System.Text.Encoding.UTF8 does.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static Action<string, string> AppendFileSync { get; internal set; } = DefaultAppendFileSync;
    public static Action<string, string> ChmodSync { get; internal set; } = DefaultChmodSync;
    public static Func<string, bool> ExistsSync { get; internal set; } = DefaultExistsSync;
    public static Func<string, bool> IsDirectorySync { get; internal set; } = DefaultIsDirectorySync;
    public static Func<string, string[]> ReadDirSync { get; internal set; } = DefaultReadDirSync;
    public static ReadFileXDelegate ReadFileX { get; internal set; } = DefaultReadFileX;
    public static Func<string, byte[]> ReadFileSync { get; internal set; } = DefaultReadFileSync;
    public static Action<string, string> WriteFileSync { get; internal set; } = DefaultWriteFileSync;
    public static Action<string, string> WriteFileX { get; internal set; } = DefaultWriteFileX;

    private static void DefaultAppendFileSync(string path, string data)
    {
        File.AppendAllText(path, data, Utf8NoBom);
    }

    private static void DefaultChmodSync(string path, string octalMode)
    {
        // .NET's File.SetUnixFileMode throws PlatformNotSupportedException on Windows (no exec-bit
        // concept there) - best-effort like ExecuteCommand.cs's own defensive style. CI runs on
        // ubuntu-latest, so this is real, verifiable behavior there, not just a no-op everywhere.
        try
        {
            UnixFileMode mode = (UnixFileMode)Convert.ToInt32(octalMode, 8);
#pragma warning disable CA1416 // guarded by the catch below, not an OperatingSystem.IsWindows() check
            File.SetUnixFileMode(path, mode);
#pragma warning restore CA1416
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    // libuv on Windows accepts a trailing separator on a file path (`fs.existsSync('.env/')` and a read both succeed); the Win32 file
    // functions .NET calls do not
    private static string WithoutTrailingSeparator(string path)
    {
        if (!OperatingSystem.IsWindows() || path.Length == 0 || !path.EndsWith('/') && !path.EndsWith('\\'))
        {
            return path;
        }

        string trimmed = path.TrimEnd('/', '\\');

        return trimmed.Length == 0 || trimmed.EndsWith(':') ? path : trimmed;
    }

    private static bool DefaultExistsSync(string path)
    {
        path = WithoutTrailingSeparator(path);

        return File.Exists(path) || Directory.Exists(path);
    }

    private static bool DefaultIsDirectorySync(string path)
    {
        // Matches fs.statSync(filepath).isDirectory() wrapped in a try/catch that swallows any
        // error (missing path, permission denied) and returns false - Directory.Exists already
        // has exactly that "false unless it's really there and really a directory" semantic, no
        // explicit try/catch needed.
        return Directory.Exists(path);
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
            // "utf16le" is what DetectEncoding.cs actually returns (matching Node's own encoding
            // name, which is always little-endian - Node has no native big-endian UTF-16 buffer
            // encoding) - .NET's Encoding.GetEncoding doesn't recognize that literal string at
            // all, so this needs its own case rather than falling through to the default.
            "utf16" or "utf16le" => System.Text.Encoding.Unicode,
            "utf32" => System.Text.Encoding.UTF32,
            _ => System.Text.Encoding.GetEncoding(encoding)
        };

        // Decode from raw bytes rather than File.ReadAllText(path, enc): that overload's
        // StreamReader auto-detects and STRIPS a leading byte-order-mark matching the requested
        // encoding, but Node's own raw Buffer decode (what detectEncoding.js/fs.readFileSync are
        // built on) does NOT strip it - a leading BOM survives as a literal U+FEFF character in the
        // decoded string on both `utf8` and `utf16le`, and gets written back out verbatim by any
        // command that rewrites the file. `Encoding.GetString(bytes)` matches that raw-decode
        // behavior (confirmed via a scratch probe: unlike File.ReadAllText, it does not strip a
        // leading BOM). This was tried once before and reverted because every `\s`-based parsing
        // regex in this port failed to recognize a key immediately after a preserved BOM (JS's `\s`
        // matches U+FEFF; .NET's doesn't) - those regexes (DotenvParseRegex, Parse.cs's LineRegex,
        // ValidateEnvExample's ScanLineRegex, Del.cs's RemoveKey, Append.cs/Replace.cs) have since
        // been updated to accept `[\s\uFEFF]*` at the relevant line-start position, so this is safe
        // now - see docs/porting-traps.md, trap on this for the full history.
        byte[] bytes = ReadFileSync(filepath);
        return enc.GetString(bytes);
    }

    private static byte[] DefaultReadFileSync(string path)
    {
        path = WithoutTrailingSeparator(path);
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (DirectoryNotFoundException error) when (!Directory.Exists(path))
        {
            // a missing parent directory is Node's ENOENT, i.e. a missing file (an existing directory keeps the native error)
            throw new FileNotFoundException(error.Message, path, error);
        }
        catch (IOException error) when (error.HResult == unchecked((int)0x8007007B))
        {
            // Windows ERROR_INVALID_NAME (a path such as ":.env"): Node's ENOENT, i.e. a missing file
            throw new FileNotFoundException(error.Message, path, error);
        }
    }

    private static void DefaultWriteFileSync(string path, string data)
    {
        File.WriteAllText(path, data);
    }

    private static void DefaultWriteFileX(string filepath, string str)
    {
        // Matches fsx.js's writeFileX/writeFileXSync: catches EACCES/EPERM specifically and
        // translates to FILE_NOT_WRITABLE - confirmed reachable live (a brand-new file in a
        // directory that denies file creation, via `set`), not the broader "any write failure"
        // this might first appear to cover. .NET surfaces this as UnauthorizedAccessException
        // (confirmed via a live probe) - a file-locked-by-another-handle sharing violation is a
        // separate IOException, deliberately NOT translated here, matching upstream's own
        // narrower EACCES/EPERM-only check.
        try
        {
            File.WriteAllText(filepath, str, Utf8NoBom);
        }
        catch (UnauthorizedAccessException)
        {
            throw new FileNotWritableException(filepath);
        }
    }

    // Method to reset to defaults for tests
    internal static void ResetToDefaults()
    {
        AppendFileSync = DefaultAppendFileSync;
        ChmodSync = DefaultChmodSync;
        ExistsSync = DefaultExistsSync;
        IsDirectorySync = DefaultIsDirectorySync;
        ReadDirSync = DefaultReadDirSync;
        ReadFileX = DefaultReadFileX;
        ReadFileSync = DefaultReadFileSync;
        WriteFileSync = DefaultWriteFileSync;
        WriteFileX = DefaultWriteFileX;
    }
}