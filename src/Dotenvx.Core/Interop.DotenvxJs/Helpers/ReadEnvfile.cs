using System.Text;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>The `Envfile` document in a directory: its path, whether it exists and its text.</summary>
internal sealed record EnvfileSource(string Filepath, bool Exists, string? Source);

/// <summary>The rules of an Envfile compiled for some env files (`{ keys, exists }`); `Keys` is empty without an Envfile.</summary>
internal sealed class EnvfileSchema
{
    public static readonly EnvfileSchema None = new() { Exists = false };

    public bool Exists { get; init; }
    public IReadOnlyDictionary<string, KeyPolicy> Keys { get; init; } = new Dictionary<string, KeyPolicy>();
    public EnvPolicy Policy { get; init; } = new();
}

/// <remarks>
/// Reference source: dotenvx/src/lib/envfile/parsing/{readEnvfile,formatEnvfileSyntaxError,resolveEnvfilePath}.js. The Envfile is
/// always the one named `Envfile` in the working directory (or the given directory); a missing one is "no Envfile", a dangling
/// link or an unreadable one is an error.
/// </remarks>
internal static class ReadEnvfile
{
    private static readonly Regex Newlines = new("\r\n|\n|\r", RegexOptions.CultureInvariant);

    private static readonly Regex Token = new("^(?:\"[^\"\r\n]*\"|'[^'\r\n]*'|[A-Za-z0-9_]+|[^\r\n])",
        RegexOptions.CultureInvariant);

    /// <summary>`resolveEnvfilePath(directory)`.</summary>
    public static string ResolvePath(string? directory = null)
    {
        return NodePath.Resolve(Path.Combine(directory ?? Directory.GetCurrentDirectory(), "Envfile"));
    }

    /// <summary>`readEnvfile.source(filepath)`.</summary>
    public static EnvfileSource Source(string? filepath = null)
    {
        filepath ??= ResolvePath();
        try
        {
            return new EnvfileSource(filepath, true, new UTF8Encoding(false).GetString(Fsx.ReadFileSync(filepath)));
        }
        catch (FileNotFoundException)
        {
            if (new FileInfo(filepath).LinkTarget is not null)
            {
                throw new Exception($"Cannot read Envfile: {filepath}");
            }

            return new EnvfileSource(filepath, false, null);
        }
    }

    /// <summary>`readEnvfile(filepath, files)`: the rules for the given env files (relative to the caller).</summary>
    public static EnvfileSchema Read(string? filepath = null, IEnumerable<string>? files = null)
    {
        EnvfileSource document = Source(filepath);
        if (!document.Exists)
        {
            return EnvfileSchema.None;
        }

        // File paths arrive relative to the caller; file blocks are relative to Envfile.
        string directory = Path.GetDirectoryName(document.Filepath) ?? ".";
        string selected = string.Join(',', (files ?? [".env"]).SelectMany(file =>
        {
            string absolute = NodePath.Resolve(file);

            return new[] { Path.GetRelativePath(directory, absolute), absolute };
        }));
        try
        {
            EnvPolicy policy = Primitives.Primitives.Policy(document.Source!, selected);

            return new EnvfileSchema { Exists = true, Keys = policy.Keys, Policy = policy };
        }
        catch (MalformedEnvfileException error)
        {
            throw FormatError(error, document);
        }
    }

    /// <summary>`readEnvfile.formatError(error, document)`: a located syntax error becomes the multi-line `file:line:col` message.</summary>
    public static Exception FormatError(MalformedEnvfileException error, EnvfileSource document)
    {
        if (error.Location is null)
        {
            return error;
        }

        return new DotenvxException(error.Code,
            $"[{error.Code}] {FormatSyntaxError(error, document.Source ?? "", document.Filepath)}");
    }

    private static string JsonString(string value)
    {
        return Helpers.JsonStringify(value);
    }

    /// <summary>formatEnvfileSyntaxError.js.</summary>
    public static string FormatSyntaxError(MalformedEnvfileException error, string src, string filepath)
    {
        EnvfileLocation location = error.Location!.Value;
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), filepath);
        string filename = relative == "." || relative.Length == 0 ? NodePath.Basename(filepath) : relative;
        string line = Newlines.Split(src).ElementAtOrDefault(location.Line - 1) ?? "";
        string message = error.CauseMessage ?? error.Message;
        if (error.Expected is not null)
        {
            string remaining = location.Offset <= src.Length ? src[location.Offset..] : "";
            Match match = Token.Match(remaining);
            string? token = match.Success ? match.Value : null;
            if (token is not null && Regex.IsMatch(remaining, "^(?:\"[^\"\r\n]*\"|'[^'\r\n]*'|[A-Za-z0-9_]+)") == false)
            {
                // a single code point (the /u flag of the original makes a surrogate pair one character)
                token = char.IsHighSurrogate(remaining[0]) && remaining.Length > 1 ? remaining[..2] : token;
            }

            string display = token is not null && (token[0] is '"' or '\'') ? token : token is null ? "" : JsonString(token);
            message = token is not null ? $"Unexpected {display}" : "Unexpected end of input";
            List<string> expected = [];
            foreach (EnvfileExpectation item in error.Expected)
            {
                string? text = item.Type switch
                {
                    "other" => item.Description,
                    "literal" when item.Text is { } literal && literal.Trim(JsWhitespace) != "" && literal != "#" => JsonString(literal),
                    _ => null
                };
                if (text is not null && !expected.Contains(text))
                {
                    expected.Add(text);
                }
            }

            if (expected.Count is > 0 and <= 3)
            {
                message += $"; expected {string.Join(" or ", expected)}";
            }
        }

        string prefix = $"{location.Line} | ";
        StringBuilder pad = new();
        foreach (char c in line[..Math.Min(line.Length, Math.Max(0, location.Column - 1))])
        {
            pad.Append(c == '\t' ? '\t' : ' ');
        }

        return $"{filename}:{location.Line}:{location.Column}: {message}\n{prefix}{line}\n{new string(' ', prefix.Length)}{pad}^";
    }


    private static readonly char[] JsWhitespace = [.. new[]
    {
        0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20, 0xA0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007,
        0x2008, 0x2009, 0x200A, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF
    }.Select(code => (char)code)];
}
