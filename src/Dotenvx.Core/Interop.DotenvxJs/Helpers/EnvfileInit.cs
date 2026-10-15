using System.Text;
using System.Text.RegularExpressions;

using Dotenvx.Interop.IgnoreJs;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>One variable of a generated Envfile (`declaration` in init.js); unset properties are not rendered.</summary>
public sealed record EnvDeclaration(
    string Name, string? Type = null, bool? Encrypted = null, bool? Redacted = null, bool? Optional = null);

public sealed record EnvfileInputFile(string Filename, bool HasValues, List<EnvDeclaration> Declarations);

public sealed record EnvfileDocument(
    List<EnvDeclaration> Declarations, List<(string Filename, List<EnvDeclaration> Declarations)> Files,
    List<EnvDeclaration> CodeDeclarations);

public sealed record InitEnvfileResult(bool Created, bool Replaced = false, string? Content = null);

/// <remarks>
/// Reference sources: dotenvx/src/lib/services/init.js, envfile/discovery/{discoverEnvFiles,scanSource,extractEnvKeys}.js
/// and envfile/rendering/{normalizeEnvfile,renderEnvfile}.js (`dotenvx spec`: create an Envfile from env files and code).
/// </remarks>
public static partial class EnvfileInit
{
    private static readonly HashSet<string> IgnoredDirectories =
        [".git", "node_modules", "dist", "build", ".next", "vendor", ".venv", "coverage", "__pycache__"];

    private static readonly HashSet<string> ProjectMarkers = ["package.json", "Envfile", ".env.schema"];

    private static readonly Dictionary<string, string> Languages = new()
    {
        ["js"] = "js", ["mjs"] = "js", ["cjs"] = "js", ["jsx"] = "js", ["ts"] = "js", ["mts"] = "js", ["cts"] = "js",
        ["tsx"] = "js", ["vue"] = "js", ["svelte"] = "js", ["astro"] = "js", ["py"] = "python", ["go"] = "go",
        ["rb"] = "ruby", ["php"] = "php", ["rs"] = "rust", ["java"] = "java", ["cs"] = "csharp"
    };

    [GeneratedRegex(@"^DOTENV_(?:PUBLIC|PRIVATE)_KEY(?:_|$)")]
    private static partial Regex DotenvKeyRegex();

    [GeneratedRegex(@"^DOTENV_PRIVATE_KEY(?:_|$)")]
    private static partial Regex PrivateKeyRegex();

    private static string Rel(string root, string filename)
    {
        return Path.GetRelativePath(root, filename).Replace(Path.DirectorySeparatorChar, '/');
    }

    // ---- discoverEnvFiles ----------------------------------------------------------------------------------------------

    /// <summary>Discovers selectable env files without reading their values (.gitignore does not hide them).</summary>
    public static List<string> DiscoverEnvFiles(string? directory = null)
    {
        string root = Path.GetFullPath(directory ?? Directory.GetCurrentDirectory());
        List<string> files = [];

        void Walk(string current)
        {
            FileSystemInfo[] entries = new DirectoryInfo(current).GetFileSystemInfos();
            // Stop at nested packages and initialized projects.
            if (current != root && entries.Any(entry => entry.Name is "Envfile" or "package.json"))
            {
                return;
            }

            foreach (FileSystemInfo entry in entries)
            {
                string filename = Path.Combine(current, entry.Name);
                bool isDirectory = entry.Attributes.HasFlag(FileAttributes.Directory) && entry.LinkTarget is null;
                if (isDirectory)
                {
                    if (!IgnoredDirectories.Contains(entry.Name) && !entry.Name.StartsWith(".env", StringComparison.Ordinal))
                    {
                        Walk(filename);
                    }

                    continue;
                }

                if (entry.Name != ".env" && !entry.Name.StartsWith(".env.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Regex.IsMatch(entry.Name, @"^\.env\.(schema|x)$") || Regex.IsMatch(entry.Name, @"^\.env\.(keys|vault)(\.|$)"))
                {
                    continue;
                }

                if (!File.Exists(filename))
                {
                    continue;
                }

                files.Add(Rel(root, filename));
            }
        }

        Walk(root);
        files.Sort(StringComparer.Ordinal);

        return files;
    }

    // ---- scanSource ----------------------------------------------------------------------------------------------------

    public static (List<string> Keys, int ScannedFiles) ScanSource(string? directory = null, Action<string>? onFile = null)
    {
        string root = Path.GetFullPath(directory ?? Directory.GetCurrentDirectory());
        SortedSet<string> keys = new(StringComparer.Ordinal);
        List<(string Filename, string Language)> files = [];

        void Walk(string dir, List<(string Directory, Ignore Matcher)> parents)
        {
            List<FileSystemInfo> entries = [.. new DirectoryInfo(dir).GetFileSystemInfos().OrderBy(e => e.Name, StringComparer.CurrentCulture)];
            if (dir != root && entries.Any(entry => entry is FileInfo && ProjectMarkers.Contains(entry.Name)))
            {
                return;
            }

            List<(string Directory, Ignore Matcher)> rules = [.. parents];
            if (entries.Any(entry => entry.Name == ".gitignore" && entry is FileInfo))
            {
                rules.Add((dir, new Ignore().Add(File.ReadAllText(Path.Combine(dir, ".gitignore"), Encoding.UTF8))));
            }

            foreach (FileSystemInfo entry in entries)
            {
                if (entry.Name.StartsWith(".env", StringComparison.Ordinal) || entry.LinkTarget is not null)
                {
                    continue;
                }

                bool isDirectory = entry is DirectoryInfo;
                if (isDirectory && IgnoredDirectories.Contains(entry.Name))
                {
                    continue;
                }

                string filename = Path.Combine(dir, entry.Name);
                bool ignored = false;
                foreach ((string ruleDirectory, Ignore matcher) in rules)
                {
                    string relative = Rel(ruleDirectory, filename) + (isDirectory ? "/" : "");
                    if (matcher.Ignores(relative))
                    {
                        ignored = true;
                    }
                }

                if (ignored)
                {
                    continue;
                }

                if (isDirectory)
                {
                    Walk(filename, rules);
                }
                else if (Languages.TryGetValue(Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant(), out string? language))
                {
                    files.Add((filename, language));
                }
            }
        }

        Walk(root, []);
        int scanned = 0;
        foreach ((string filename, string language) in files)
        {
            if (new FileInfo(filename).Length > 1024 * 1024)
            {
                continue;
            }

            string content = File.ReadAllText(filename, Encoding.UTF8);
            if (content.Contains('\0'))
            {
                continue;
            }

            onFile?.Invoke(Rel(root, filename));
            scanned++;
            foreach (string key in ExtractEnvKeys(content, language).Where(key => !DotenvKeyRegex().IsMatch(key)))
            {
                keys.Add(key);
            }
        }

        return ([.. keys], scanned);
    }

    // ---- extractEnvKeys ------------------------------------------------------------------------------------------------

    private const string Identifier = "[A-Za-z_][A-Za-z0-9_]*";
    private const string Literal = $"(['\"`])({Identifier})\\1";
    private const string Member = "\\s*(?:\\?\\.|\\.)\\s*";
    private const string EnvMember = $"(?:{Member}env|\\s*(?:\\?\\.\\s*)?\\[\\s*(?:'env'|\"env\")\\s*\\])";
    private const string JsEnv = $"(?:process{EnvMember}|import{Member}meta{EnvMember})";
    private const string Boundary = "(?<![\\w$.])";

    private static string EscapeRegex(string value)
    {
        return Regex.Replace(value, @"[.*+?^${}()|[\]\\]", "\\$&");
    }

    private static Dictionary<int, int> OpeningBraces(string source)
    {
        Dictionary<int, int> openings = [];
        Stack<int> stack = new();
        char quote = '\0';
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quote != '\0')
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"' or '`')
            {
                quote = c;
            }
            else if (string.CompareOrdinal(source, i, "//", 0, 2) == 0)
            {
                int end = source.IndexOf('\n', i + 2);
                i = end == -1 ? source.Length : end;
            }
            else if (string.CompareOrdinal(source, i, "/*", 0, 2) == 0)
            {
                int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end == -1 ? source.Length : end + 1;
            }
            else if (c == '{')
            {
                stack.Push(i);
            }
            else if (c == '}')
            {
                openings[i] = stack.Count > 0 ? stack.Pop() : -1;
            }
        }

        return openings;
    }

    private static List<string> Properties(string body)
    {
        List<string> parts = [];
        int start = 0;
        int depth = 0;
        char quote = '\0';
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (quote != '\0')
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"' or '`')
            {
                quote = c;
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                parts.Add(body[start..i]);
                start = i + 1;
            }
        }

        parts.Add(body[start..]);

        return parts;
    }

    /// <summary>Best-effort static syntax extraction of env key names (does not evaluate expressions).</summary>
    public static List<string> ExtractEnvKeys(string content, string language)
    {
        string source = string.Join('\n', content.Split('\n').Select(line =>
        {
            string text = line.TrimStart();
            if (language is "python" or "ruby" or "php" && text.StartsWith('#'))
            {
                return "";
            }

            if (language is not ("python" or "ruby") && Regex.IsMatch(text, @"^(//|/\*|\*(?:\s|/|$))"))
            {
                return "";
            }

            return line;
        }));
        SortedSet<string> keys = new(StringComparer.Ordinal);

        void Collect(string pattern, int group = 2, string? input = null)
        {
            foreach (Match match in Regex.Matches(input ?? source, pattern))
            {
                keys.Add(match.Groups[group].Value);
            }
        }

        void Call(string baseExpression)
        {
            Collect($"{Boundary}(?:{baseExpression})\\s*\\(\\s*{Literal}\\s*(?=[,)])");
        }

        void Bracket(string baseExpression)
        {
            Collect($"{Boundary}(?:{baseExpression})\\s*\\[\\s*{Literal}\\s*\\]");
        }

        switch (language)
        {
            case "js":
                {
                    List<(string Base, int Start)> bases = [(JsEnv, 0)];
                    foreach (Match match in Regex.Matches(source, @"\bconst\s+([A-Za-z_$][\w$]*)\s*=\s*([^;\n]+)"))
                    {
                        string value = match.Groups[2].Value.Trim();
                        if (!bases.Any(item => Regex.IsMatch(value, $"^(?:{item.Base})$")))
                        {
                            continue;
                        }

                        bases.Add((EscapeRegex(match.Groups[1].Value), match.Index + match.Length));
                    }

                    foreach ((string baseExpression, int start) in bases)
                    {
                        string input = source[start..];
                        Collect($"{Boundary}(?:{baseExpression}){Member}({Identifier})\\b", 1, input);
                        Collect($"{Boundary}(?:{baseExpression})\\s*(?:\\?\\.\\s*)?\\[\\s*{Literal}\\s*\\]", 2, input);
                        Dictionary<int, int> openings = OpeningBraces(input);
                        foreach (Match match in Regex.Matches(input,
                                     $"}}\\s*=\\s*(?:{baseExpression})(?![\\w$]|\\s*(?:\\?\\.|\\.|\\[))"))
                        {
                            if (!openings.TryGetValue(match.Index, out int opening) || opening < 0)
                            {
                                continue;
                            }

                            foreach (string property in Properties(input[(opening + 1)..match.Index]))
                            {
                                Match key = Regex.Match(property.Trim(),
                                    $"^(?:({Identifier})|['\"]({Identifier})['\"]|\\[\\s*['\"]({Identifier})['\"]\\s*\\])\\s*(?=[:,=]|$)");
                                if (key.Success)
                                {
                                    keys.Add(key.Groups[1].Success && key.Groups[1].Value != "" ? key.Groups[1].Value
                                        : key.Groups[2].Success && key.Groups[2].Value != "" ? key.Groups[2].Value
                                        : key.Groups[3].Value);
                                }
                            }
                        }
                    }

                    break;
                }
            case "python":
                {
                    HashSet<string> modules = ["os"];
                    HashSet<string> getters = [];
                    HashSet<string> environs = [];
                    foreach (Match match in Regex.Matches(source, @"^\s*import\s+([^\n;]+)", RegexOptions.Multiline))
                    {
                        foreach (string item in match.Groups[1].Value.Split(','))
                        {
                            Match imported = Regex.Match(item.Trim(), @"^os(?:\s+as\s+([A-Za-z_]\w*))?\s*(?:#.*)?$");
                            if (imported.Success)
                            {
                                modules.Add(imported.Groups[1].Success ? imported.Groups[1].Value : "os");
                            }
                        }
                    }

                    foreach (Match match in Regex.Matches(source, @"^\s*from\s+os\s+import\s+(?:\(([^)]*)\)|([^\n;]+))", RegexOptions.Multiline))
                    {
                        string list = match.Groups[1].Success && match.Groups[1].Value != "" ? match.Groups[1].Value : match.Groups[2].Value;
                        foreach (string item in list.Split(','))
                        {
                            Match imported = Regex.Match(Regex.Replace(item, "#[^\n]*", "").Trim(),
                                @"^(getenv|environ)(?:\s+as\s+([A-Za-z_]\w*))?$");
                            if (!imported.Success)
                            {
                                continue;
                            }

                            (imported.Groups[1].Value == "getenv" ? getters : environs).Add(
                                imported.Groups[2].Success ? imported.Groups[2].Value : imported.Groups[1].Value);
                        }
                    }

                    foreach (string name in modules)
                    {
                        Call($"{EscapeRegex(name)}\\s*\\.\\s*getenv");
                        environs.Add($"{name}.environ");
                    }

                    foreach (string name in getters)
                    {
                        Call(EscapeRegex(name));
                    }

                    foreach (string name in environs)
                    {
                        string baseExpression = string.Join("\\s*\\.\\s*", name.Split('.').Select(EscapeRegex));
                        Bracket(baseExpression);
                        Call($"{baseExpression}\\s*\\.\\s*get");
                    }

                    break;
                }
            case "go":
                Call("os\\s*\\.\\s*(?:Getenv|LookupEnv)");
                foreach (Match match in Regex.Matches(source, @"\bimport\s+(?:\(([^)]*)\)|([^\n;]+))"))
                {
                    string list = match.Groups[1].Success && match.Groups[1].Value != "" ? match.Groups[1].Value : match.Groups[2].Value;
                    foreach (Match imported in Regex.Matches(list, @"(?:^|[;\n])\s*([A-Za-z_]\w*|\.)\s+[""`]os[""`]\s*(?=$|[;\n]|//)"))
                    {
                        string alias = imported.Groups[1].Value;
                        if (alias == "_")
                        {
                            continue;
                        }

                        Call(alias == "." ? "(?:Getenv|LookupEnv)" : $"{EscapeRegex(alias)}\\s*\\.\\s*(?:Getenv|LookupEnv)");
                    }
                }

                break;
            case "ruby":
                Bracket("ENV");
                Call("ENV\\s*\\.\\s*fetch");
                Collect($"{Boundary}ENV\\s*\\.\\s*fetch\\s+{Literal}\\s*(?=,|$|[;\\r\\n])");
                break;
            case "php":
                Call("getenv");
                Bracket("\\$_(?:ENV|SERVER)");
                break;
            case "rust":
                {
                    Call("(?:std\\s*::\\s*)?env\\s*::\\s*(?:var|var_os)");

                    void Imports(string tree, string prefix = "")
                    {
                        foreach (string item in Properties(tree))
                        {
                            string value = item.Trim();
                            int group = value.IndexOf('{');
                            if (group != -1 && value.EndsWith('}'))
                            {
                                Imports(value[(group + 1)..^1], prefix + value[..group]);

                                continue;
                            }

                            string[] split = Regex.Split(prefix + value, @"\s+as\s+");
                            string? alias = split.Length > 1 ? split[1] : null;
                            string path = Regex.Replace(Regex.Replace(split[0], @"\s", ""), "::self$", "");
                            if (alias == "_")
                            {
                                continue;
                            }

                            if (path == "std::env")
                            {
                                Call($"{EscapeRegex(alias ?? "env")}\\s*::\\s*(?:var|var_os)");
                            }

                            if (Regex.IsMatch(path, "^std::env::(?:var|var_os)$"))
                            {
                                Call(EscapeRegex(alias ?? path.Split("::")[^1]));
                            }
                        }
                    }

                    foreach (Match match in Regex.Matches(source, @"\buse\s+([^;]+);"))
                    {
                        Imports(match.Groups[1].Value);
                    }

                    break;
                }
            case "java":
                Call("(?:java\\s*\\.\\s*lang\\s*\\.\\s*)?System\\s*\\.\\s*getenv");
                break;
            case "csharp":
                Call("(?:System\\s*\\.\\s*)?Environment\\s*\\.\\s*GetEnvironmentVariable");
                break;
        }

        return [.. keys];
    }

    // ---- normalizeEnvfile / renderEnvfile ------------------------------------------------------------------------------

    public static EnvfileDocument NormalizeEnvfile(List<EnvfileInputFile> files, List<EnvDeclaration> codeDeclarations)
    {
        bool hasNamedEnvironment = files.Any(file => file.Filename is not (".env" or ".env.example"));
        List<EnvfileInputFile> rootFiles = [];
        List<EnvfileInputFile> environments = [];
        foreach (EnvfileInputFile file in files)
        {
            if (file.Filename == ".env.example" || (file.Filename == ".env" && !hasNamedEnvironment))
            {
                rootFiles.Add(file);
            }
            else
            {
                environments.Add(file);
            }
        }

        OrderedDictionary<string, EnvDeclaration> root = new();
        foreach (EnvfileInputFile file in rootFiles)
        {
            foreach (EnvDeclaration item in file.Declarations)
            {
                bool previous = root.TryGetValue(item.Name, out EnvDeclaration? prev) && prev.Encrypted == true;
                root[item.Name] = item with { Encrypted = previous || item.Encrypted == true };
            }
        }

        if (environments.Count > 1)
        {
            foreach (EnvDeclaration item in environments[0].Declarations)
            {
                List<EnvDeclaration?> matches = [.. environments.Select(file => file.Declarations.FirstOrDefault(other => other.Name == item.Name))];
                if (!root.ContainsKey(item.Name) && matches.All(other => other is not null && other.Type == item.Type && other.Optional == item.Optional))
                {
                    // Keep encryption differences in their file scopes, not the shared declaration.
                    root[item.Name] = item with { Encrypted = matches.Any(other => other!.Encrypted != false) };
                }
            }
        }

        List<(string, List<EnvDeclaration>)> blocks = [];
        foreach (EnvfileInputFile file in environments)
        {
            List<EnvDeclaration> declarations = [];
            foreach (EnvDeclaration item in file.Declarations)
            {
                bool inherited = root.TryGetValue(item.Name, out EnvDeclaration? parent);
                EnvDeclaration defaults = parent ?? new EnvDeclaration(item.Name, Encrypted: true);
                string? type = item.Type != defaults.Type ? item.Type : null;
                bool? encrypted = item.Encrypted != defaults.Encrypted ? item.Encrypted : null;
                bool? redacted = item.Redacted != defaults.Redacted ? item.Redacted : null;
                bool? optional = item.Optional != defaults.Optional ? item.Optional : null;
                if (inherited && type is null && encrypted is null && redacted is null && optional is null)
                {
                    continue;
                }

                declarations.Add(new EnvDeclaration(item.Name, type, encrypted, redacted, optional));
            }

            blocks.Add((file.Filename, declarations));
        }

        HashSet<string> names = [.. files.SelectMany(file => file.Declarations.Select(item => item.Name))];
        List<EnvDeclaration> code = [];
        foreach (EnvDeclaration item in codeDeclarations)
        {
            if (!names.Add(item.Name))
            {
                continue;
            }

            code.Add(item with { Optional = true });
        }

        List<EnvDeclaration> declarationsOut = [.. root.Values.Select(item => item.Encrypted == true ? item with { Encrypted = null } : item)];

        return new EnvfileDocument(declarationsOut, blocks, code);
    }

    private static string Render(EnvDeclaration item)
    {
        string line = $"env \"{item.Name}\"";
        if (item.Type is not null)
        {
            line += $", type: \"{item.Type}\"";
        }

        if (item.Encrypted is not null)
        {
            line += $", encrypted: {(item.Encrypted == true ? "true" : "false")}";
        }

        if (item.Redacted is not null)
        {
            line += $", redacted: {(item.Redacted == true ? "true" : "false")}";
        }

        if (item.Optional is not null)
        {
            line += $", optional: {(item.Optional == true ? "true" : "false")}";
        }

        return line;
    }

    public static string RenderEnvfile(EnvfileDocument document)
    {
        List<string> sections = ["strict true"];
        if (document.Declarations.Count > 0)
        {
            sections.Add(string.Join('\n', document.Declarations.Select(Render)));
        }

        foreach ((string filename, List<EnvDeclaration> declarations) in document.Files)
        {
            string quote = filename.Contains('"') ? "'" : "\"";
            if (filename.Contains(quote) || Regex.IsMatch(filename, @"[\r\n\0*?\[\]{}]"))
            {
                throw new Exception($"Unsupported Envfile filename: {filename}");
            }

            List<string> lines = [$"file {quote}{filename}{quote} do", .. declarations.Select(item => $"  {Render(item)}"), "end"];
            sections.Add(string.Join('\n', lines));
        }

        if (document.CodeDeclarations.Count > 0)
        {
            sections.Add("# additionally found in code\n" + string.Join('\n', document.CodeDeclarations.Select(Render)));
        }

        return "# Envfile (safe to commit)\n# ------------------------\n\n" + string.Join("\n\n", sections) + "\n";
    }

    // ---- init ----------------------------------------------------------------------------------------------------------

    private static EnvDeclaration Declaration(string name)
    {
        string? type = null;
        if (Regex.IsMatch(name, @"(^|_)port$", RegexOptions.IgnoreCase))
        {
            type = "port";
        }

        if (Regex.IsMatch(name, @"(^|_)url$", RegexOptions.IgnoreCase))
        {
            type = "url";
        }

        return new EnvDeclaration(name, type, Redacted: Helpers.IsPublicKeyName(name) ? false : null);
    }

    public static InitEnvfileResult Init(string? directory = null, string? envFile = null, IReadOnlyList<string>? envFiles = null,
        IReadOnlyList<string>? sourceKeys = null, bool overwrite = false, bool stdout = false, Action<string>? onFile = null)
    {
        directory ??= Directory.GetCurrentDirectory();
        string target = Path.GetFullPath(Path.Combine(directory, "Envfile"));
        FileSystemInfo? existing = stdout ? null : Lstat(target);
        if (existing is not null && !overwrite)
        {
            return new InitEnvfileResult(false);
        }

        if (existing is not null && existing is not FileInfo)
        {
            throw new Exception($"Cannot replace Envfile: expected a regular file ({target})");
        }

        HashSet<string> seen = [];
        HashSet<string> names = [];
        List<EnvfileInputFile> files = [];
        IReadOnlyList<string> candidates = envFiles ?? (envFile is not null ? [envFile] : [".env.example", ".env"]);
        foreach (string candidate in candidates)
        {
            string resolved = Path.GetFullPath(Path.Combine(directory, candidate));
            if (!seen.Add(resolved))
            {
                continue;
            }

            string src;
            try
            {
                src = File.ReadAllText(resolved, Encoding.UTF8);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                if (envFiles is not null || envFile is not null)
                {
                    throw new FileNotFoundException(error.Message, resolved);
                }

                continue;
            }

            onFile?.Invoke(candidate);
            List<KeyValuePair<string, List<string>>> entries = [.. Primitives.Primitives.Scan(src, new ScanOptions()).Parsed
                .Where(pair => !pair.Key.StartsWith("DOTENV_PUBLIC_KEY", StringComparison.Ordinal))];
            foreach ((string key, _) in entries.Where(pair => !PrivateKeyRegex().IsMatch(pair.Key)))
            {
                names.Add(key);
            }

            bool hasEncryptedValues = entries.Any(pair => !PrivateKeyRegex().IsMatch(pair.Key) && pair.Value.Any(v => Primitives.Primitives.Encrypted(v)));
            files.Add(new EnvfileInputFile(
                Path.GetRelativePath(Path.GetFullPath(directory), resolved).Replace(Path.DirectorySeparatorChar, '/'),
                entries.Count > 0,
                [.. entries.Where(pair => !PrivateKeyRegex().IsMatch(pair.Key)).Select(pair => Declaration(pair.Key) with
                {
                    Encrypted = pair.Value.Any(v => Primitives.Primitives.Encrypted(v)) ||
                                (!hasEncryptedValues && !pair.Key.EndsWith("_PLAIN", StringComparison.Ordinal))
                })]));
        }

        List<EnvDeclaration> codeDeclarations = [.. (sourceKeys ?? []).Where(key => !DotenvKeyRegex().IsMatch(key)).Select(Declaration)];
        foreach (EnvDeclaration item in codeDeclarations)
        {
            names.Add(item.Name);
        }

        string[] invalid = [.. names.Where(key => !Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$"))];
        if (invalid.Length > 0)
        {
            throw new Exception($"Unsupported Envfile variable names: {string.Join(", ", invalid)}");
        }

        string content = RenderEnvfile(NormalizeEnvfile(files, codeDeclarations));
        if (stdout)
        {
            return new InitEnvfileResult(false, Content: content);
        }

        try
        {
            using FileStream stream = new(target, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (IOException) when (!overwrite && File.Exists(target))
        {
            return new InitEnvfileResult(false);
        }

        return new InitEnvfileResult(true, existing is not null);
    }

    private static FileSystemInfo? Lstat(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path);
        }

        return Directory.Exists(path) ? new DirectoryInfo(path) : null;
    }
}
