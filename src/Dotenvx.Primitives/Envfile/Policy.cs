using System.Numerics;

namespace Dotenvx.Primitives;

/// <summary>The `env` part of a key's compiled policy.</summary>
public sealed class EnvRule
{
    public string Type { get; init; } = "string";
    public string? Min { get; init; }
    public string? Max { get; init; }
    public List<string>? Enum { get; init; }
    public bool Required { get; init; }
    public bool Strict { get; init; }
}

/// <summary>The compiled policy of one key: `env` rules, whether it must be encrypted (`fs`) and redacted (`stdio`).</summary>
public sealed class KeyPolicy
{
    public EnvRule Env { get; init; } = new();
    public bool Encrypted { get; init; } = true;
    public bool Redacted { get; init; } = true;
}

/// <summary>The result of <see cref="Primitives.Policy" />: the compiled policy of every key, in declaration order.</summary>
public sealed class EnvPolicy
{
    public OrderedDictionary<string, KeyPolicy> Keys { get; } = [];
}

internal sealed class EnvfileModel
{
    public EnvfileDocument Document { get; init; } = new();
    public Dictionary<string, EnvfileFileBlock> Blocks { get; } = [];
    public Dictionary<string, List<EnvDeclaration>> BlockItems { get; } = [];
    public OrderedDictionary<string, EnvDeclaration> Base { get; } = [];
    public OrderedDictionary<string, List<string>> Profiles { get; } = [];
    public Func<string, string> Normalize { get; init; } = value => value;
}

/// <remarks>
/// Counterpart of the @dotenvx/primitives `policy` and `profile` exports (the Envfile model, its checks and the
/// compilation of a key's rules). Read from the 3.3.0 source and checked against the real package by the oracle tests.
/// </remarks>
public static partial class Primitives
{
    /// <summary>Compiles Envfile text into the rules of every key; for comma-separated files the first file wins explicit rules.</summary>
    public static EnvPolicy Policy(string source, string file = ".env")
    {
        EnvfileModel model = LoadEnvfile(source);
        EnvPolicy policy = new();
        foreach ((string key, KeyPolicy rule) in Select(model, file))
        {
            policy.Keys[key] = rule;
        }

        return policy;
    }

    internal static EnvfileModel LoadEnvfile(string source)
    {
        EnvfileDocument document = EnvfileParser.Parse(source.StartsWith('\uFEFF') ? source[1..] : source);
        EnvfileModel model = new() { Document = document, Normalize = value => NormalizePath(EnvfileFilename(value)) };

        foreach (EnvDeclaration declaration in Declarations(document.Declarations, ""))
        {
            model.Base[declaration.Name] = declaration;
        }

        foreach (EnvDeclaration declaration in model.Base.Values)
        {
            Compile(declaration);
        }

        foreach (EnvfileFileBlock block in document.Files)
        {
            string normalized = model.Normalize(block.File);
            if (model.Blocks.ContainsKey(normalized))
            {
                throw new MalformedEnvfileException($"Duplicate Envfile file block: {block.File}");
            }

            List<EnvDeclaration> items = Declarations(block.Declarations, $" in {block.File}");
            foreach (EnvDeclaration item in items)
            {
                EnvDeclaration merged = model.Base.TryGetValue(item.Name, out EnvDeclaration? parent)
                    ? parent.MergedWith(item)
                    : item;
                Compile(merged);
            }

            model.Blocks[normalized] = block;
            model.BlockItems[normalized] = items;
        }

        foreach (EnvfileProfile profile in document.Profiles)
        {
            if (profile.Profile.Trim().Length == 0 || profile.Profile != profile.Profile.Trim())
            {
                throw new MalformedEnvfileException("Profile names must be nonempty and have no surrounding whitespace");
            }

            if (model.Profiles.ContainsKey(profile.Profile))
            {
                throw new MalformedEnvfileException($"Duplicate profile declaration: {profile.Profile}");
            }

            model.Profiles[profile.Profile] = [.. profile.Paths.Select(EnvfileFilename)];
        }

        return model;
    }

    private static List<EnvDeclaration> Declarations(List<EnvDeclaration> declarations, string where)
    {
        HashSet<string> seen = [];
        foreach (EnvDeclaration declaration in declarations)
        {
            if (!seen.Add(declaration.Name))
            {
                throw new MalformedEnvfileException($"Duplicate Envfile declaration{where}: {declaration.Name}");
            }
        }

        return declarations;
    }

    /// <summary>A file name in a file block or profile: nonempty and literal (no commas, glob characters or interpolation).</summary>
    internal static string EnvfileFilename(string value)
    {
        if (JsTrim(value).Length == 0 || value.AsSpan().IndexOfAny("\0\r\n,*?[]") >= 0 || value.Contains("${"))
        {
            throw new MalformedEnvfileException(
                "File selections require nonempty literal filenames, without commas, glob patterns or interpolation");
        }

        return JsTrim(value);
    }

    /// <summary>A comma-separated file selection as a list of validated names.</summary>
    internal static List<string> SplitFiles(string value)
    {
        return value.Length == 0 ? [] : [.. value.Split(',').Select(EnvfileFilename)];
    }

    private static readonly Func<string, bool>? None = null;

    private static BigInteger Big(string value)
    {
        return BigInteger.Parse(value, System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Compile(EnvDeclaration declaration, out bool required, out bool encrypted,
        out bool redacted, out string? min, out string? max, out List<string>? choices)
    {
        string name = declaration.Name;
        required = declaration.Required ?? true;
        string type = declaration.Type ?? "string";
        encrypted = declaration.Encrypted ?? true;
        redacted = declaration.Redacted ?? true;
        string? f = declaration.Min;
        string? l = declaration.Max;
        if (f != null || l != null)
        {
            if (type is not ("string" or "integer" or "port"))
            {
                throw new MalformedEnvfileException(
                    $"Invalid Envfile range for {name}: min and max require string, integer, or port");
            }

            if (type == "string" && new[] { f, l }.Any(h => h != null && Big(h) < 0))
            {
                throw new MalformedEnvfileException($"Invalid Envfile length for {name}: bounds must be nonnegative");
            }
        }

        if (type == "port")
        {
            if (f == null || Big(f) < 0)
            {
                f = "0";
            }

            if (l == null || Big(l) > 65535)
            {
                l = "65535";
            }
        }

        if (f != null && l != null && Big(f) > Big(l))
        {
            throw new MalformedEnvfileException($"Invalid Envfile range for {name}: min must be less than or equal to max");
        }

        min = f != null ? Big(f).ToString() : null;
        max = l != null ? Big(l).ToString() : null;
        choices = null;
        if (declaration.Enum != null)
        {
            Func<string, bool>? valid = type switch
            {
                "integer" => value => IsInteger(value),
                "port" => value => IsInteger(value) && Big(JsTrim(value)) >= 0 && Big(JsTrim(value)) <= 65535,
                "boolean" => IsBooleanValue,
                "url" => IsUrl,
                "email" => IsEmail,
                "ip" => IsIp,
                _ => None
            };
            if (valid != null && declaration.Enum.Any(choice => !valid(choice)))
            {
                throw new MalformedEnvfileException($"Invalid Envfile enum for {name}: expected {type} choices");
            }

            choices = [.. declaration.Enum];
        }

        return type;
    }

    private static void Compile(EnvDeclaration declaration)
    {
        Compile(declaration, out _, out _, out _, out _, out _, out _);
    }

    private static KeyPolicy CompileKey(EnvDeclaration declaration, bool strict)
    {
        string type = Compile(declaration, out bool required, out bool encrypted, out bool redacted, out string? min,
            out string? max, out List<string>? choices);
        return new KeyPolicy
        {
            Env = new EnvRule { Type = type, Min = min, Max = max, Enum = choices, Required = required, Strict = strict },
            Encrypted = encrypted,
            Redacted = redacted
        };
    }

    /// <summary>The rules for the selected files: declarations from the base, then file blocks (the first listed file wins).</summary>
    private static IEnumerable<(string Key, KeyPolicy Rule)> Select(EnvfileModel model, string files)
    {
        List<string> selected = [.. SplitFiles(files).Select(model.Normalize).Distinct()];
        OrderedDictionary<string, EnvDeclaration> merged = [];
        foreach ((string key, EnvDeclaration value) in model.Base)
        {
            merged[key] = value;
        }

        bool strict = model.Document.Strict ?? false;
        selected.Reverse();
        foreach (string file in selected)
        {
            if (!model.Blocks.TryGetValue(file, out EnvfileFileBlock? block))
            {
                continue;
            }

            if (block.Strict != null)
            {
                strict = block.Strict.Value;
            }

            foreach (EnvDeclaration item in model.BlockItems[file])
            {
                merged[item.Name] = merged.TryGetValue(item.Name, out EnvDeclaration? existing)
                    ? existing.MergedWith(item)
                    : item;
            }
        }

        return merged.Select(pair => (pair.Key, CompileKey(pair.Value, strict))).ToList();
    }

    // path.normalize for the host platform (what the Envfile file names are compared by)
    internal static string NormalizePath(string path)
    {
        bool windows = OperatingSystem.IsWindows();
        if (path.Length == 0)
        {
            return ".";
        }

        char separator = windows ? '\\' : '/';
        string text = windows ? path.Replace('/', '\\') : path;
        string root = "";
        string rest = text;
        if (windows)
        {
            if (text.Length >= 2 && text[1] == ':' && char.IsAsciiLetter(text[0]))
            {
                root = text[..2];
                rest = text[2..];
            }
            else if (text.StartsWith("\\\\") && text.Length > 2)
            {
                // UNC: \\server\share
                string[] unc = text[2..].Split('\\', 3);
                if (unc.Length >= 2 && unc[0].Length > 0 && unc[1].Length > 0)
                {
                    root = "\\\\" + unc[0] + "\\" + unc[1];
                    rest = unc.Length == 3 ? "\\" + unc[2] : "";
                }
            }
        }

        bool absolute = rest.StartsWith(separator);
        bool trailing = rest.EndsWith(separator);
        List<string> parts = [];
        foreach (string part in rest.Split(separator))
        {
            if (part.Length == 0 || part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (parts.Count > 0 && parts[^1] != "..")
                {
                    parts.RemoveAt(parts.Count - 1);
                }
                else if (!absolute)
                {
                    parts.Add("..");
                }

                continue;
            }

            parts.Add(part);
        }

        string joined = string.Join(separator, parts);
        if (joined.Length == 0 && !absolute)
        {
            joined = root.Length > 0 && !absolute ? "" : ".";
        }

        if (joined.Length > 0 && trailing)
        {
            joined += separator;
        }

        string prefix = root + (absolute ? separator.ToString() : "");
        string result = prefix + joined;
        return result.Length == 0 ? "." : result;
    }

    private static bool IsInteger(string value)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(JsTrim(value), @"^[+-]?[0-9]+\z");
    }

    private static bool IsBooleanValue(string value)
    {
        return value is "true" or "false" or "1" or "0";
    }
}
