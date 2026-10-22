using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>A failed git invocation (`execFileSync` throws with `error.status`).</summary>
internal sealed class GitCommandException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

/// <remarks>
/// Reference sources: dotenvx/src/lib/helpers/{installProtectFilter,installProtectIgnore,protectSettings,
/// removeLegacyProtectFilter,uninstallPrecommitHook,plaintextKeys,precommitEnvPatterns}.js and
/// envfile/validation/protectEnvfile.js (`dotenvx protect`: Git-level protection of plaintext secrets and private keys).
/// </remarks>
internal static partial class Protect
{
    public static readonly string[] PrecommitEnvPatterns = [".env*", "*.env", ".flaskenv", ".dev.vars*", ".env.d/*"];

    private static string Attribute(string pattern, string driver)
    {
        return $"{(pattern.Contains('/') ? "**/" : "")}{pattern} filter={driver}";
    }

    /// <summary>`execFileSync('git', args)`: trimmed stdout, or a <see cref="GitCommandException" /> with git's exit status.</summary>
    public static string Git(IEnumerable<string> args, string? cwd = null)
    {
        ProcessStartInfo info = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = cwd ?? Directory.GetCurrentDirectory()
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(info) ?? throw new GitCommandException(127, "git not found");
        process.StandardInput.Close();
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new GitCommandException(process.ExitCode, error.Result);
        }

        return output.Trim();
    }

    private static string GlobalGit(params string[] args)
    {
        return Git(["config", "--global", .. args]);
    }

    private static string GetConfig(string key, bool pathname = false)
    {
        try
        {
            return GlobalGit(["--includes", .. pathname ? new[] { "--path" } : [], "--get", key]);
        }
        catch (GitCommandException error) when (error.Status == 1)
        {
            return "";
        }
    }

    /// <summary>`path.join`: segments joined with the platform separator (separators normalized).</summary>
    private static string JoinPath(params string[] parts)
    {
        return Path.Combine(parts).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string ConfigDirectory()
    {
        string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        return !string.IsNullOrEmpty(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    }

    private static string ConfigFile(string key, string name)
    {
        string configured = GetConfig(key, true);

        return configured != "" ? configured : JoinPath(ConfigDirectory(), "git", name);
    }

    private static string Read(string filename)
    {
        return File.Exists(filename) ? File.ReadAllText(filename, Encoding.UTF8) : "";
    }

    /// <summary>`source.split(/(?&lt;=\n)/)`: lines with their terminators kept.</summary>
    private static List<string> SplitLines(string source)
    {
        List<string> lines = [];
        int start = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                lines.Add(source[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < source.Length)
        {
            lines.Add(source[start..]);
        }

        return lines;
    }

    private static string TrimEol(string line)
    {
        return Regex.Replace(line, "\r?\n$", "");
    }

    private static void RemoveLines(string filename, IReadOnlyCollection<string> lines)
    {
        string current = Read(filename);
        string updated = string.Concat(SplitLines(current).Where(line => !lines.Contains(TrimEol(line))));
        if (updated != current)
        {
            Fsx.WriteFileX(filename, updated);
        }
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "'\\''") + "'";
    }

    private static string Executable()
    {
        string path = Environment.ProcessPath ?? "dotenvx";
        if (Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            System.Reflection.Assembly.GetEntryAssembly()?.Location is { Length: > 0 } dll)
        {
            return $"{Quote(path)} {Quote(dll)}";
        }

        return Quote(path);
    }

    /// <summary>installProtectFilter.js: registers the Git clean/process filter and its attributes globally.</summary>
    public static string InstallProtectFilter()
    {
        string attributesPath = "";
        try
        {
            attributesPath = Git(["config", "--global", "--includes", "--path", "--get", "core.attributesFile"]);
        }
        catch (GitCommandException error) when (error.Status == 1)
        {
            // not configured
        }

        if (attributesPath == "")
        {
            attributesPath = JoinPath(ConfigDirectory(), "git/attributes");
            GlobalGit("core.attributesFile", attributesPath);
        }

        string current = File.Exists(attributesPath) ? File.ReadAllText(attributesPath, Encoding.UTF8) : "";
        string executable = Executable();
        string[] attributes = [.. PrecommitEnvPatterns.Select(pattern => Attribute(pattern, "dotenvx.protect"))];

        // Git shell-quotes the filename itself when substituting %f; keep the placeholder unquoted here.
        GlobalGit("filter.dotenvx.clean", $"{executable} protect --git-file %f");
        GlobalGit("filter.dotenvx.process", $"{executable} protect --git-process");
        GlobalGit("filter.dotenvx.required", "true");
        // Keep the old driver working for repositories with older attributes.
        GlobalGit("filter.dotenvx.protect.clean", $"{executable} protect --git-file %f");
        GlobalGit("filter.dotenvx.protect.process", $"{executable} protect --git-process");
        GlobalGit("filter.dotenvx.protect.required", "true");
        Directory.CreateDirectory(Path.GetDirectoryName(attributesPath)!);
        string[] oldAttributes = [.. PrecommitEnvPatterns.Select(pattern => Attribute(pattern, "dotenvx"))];
        string migrated = string.Concat(SplitLines(current).Select(line =>
            oldAttributes.Contains(TrimEol(line))
                ? line.Replace("filter=dotenvx", "filter=dotenvx.protect", StringComparison.Ordinal)
                : line));
        HashSet<string> existing = [.. Regex.Split(migrated, "\r?\n")];
        string[] missing = [.. attributes.Where(attribute => !existing.Contains(attribute))];
        string next = migrated;
        if (missing.Length > 0)
        {
            next = $"{next}{(next != "" && !next.EndsWith('\n') ? "\n" : "")}{string.Join('\n', missing)}\n";
        }

        if (next != current)
        {
            Fsx.WriteFileX(attributesPath, next);
        }

        return attributesPath;
    }

    /// <summary>installProtectIgnore.js: adds `.env.keys*` to the global Git ignore file.</summary>
    public static string InstallProtectIgnore()
    {
        string ignorePath = "";
        try
        {
            ignorePath = Git(["config", "--global", "--includes", "--path", "--get", "core.excludesFile"]);
        }
        catch (GitCommandException error) when (error.Status == 1)
        {
            // not configured
        }

        if (ignorePath == "")
        {
            ignorePath = JoinPath(ConfigDirectory(), "git/ignore");
            GlobalGit("core.excludesFile", ignorePath);
        }

        string current = File.Exists(ignorePath) ? File.ReadAllText(ignorePath, Encoding.UTF8) : "";
        if (!Regex.Split(current, "\r?\n").Contains(".env.keys*"))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ignorePath)!);
            File.AppendAllText(ignorePath, $"{(current != "" && !current.EndsWith('\n') ? "\n" : "")}.env.keys*\n",
                new UTF8Encoding(false));
            GlobalGit("dotenvx.protect.ignoreFile", ignorePath);
        }

        return ignorePath;
    }

    public sealed record ProtectState(bool Filter, bool Ignore, bool Configured);

    [GeneratedRegex(" protect --git-process$")]
    private static partial Regex ProcessFilterRegex();

    [GeneratedRegex(@"(?:protect --git-file|precommit --clean) %f$")]
    private static partial Regex CleanFilterRegex();

    public static ProtectState State()
    {
        string attributes = Read(ConfigFile("core.attributesFile", "attributes"));
        bool filter = new[] { "dotenvx", "dotenvx.protect" }.Any(driver =>
        {
            string clean = GetConfig($"filter.{driver}.clean");
            string processFilter = GetConfig($"filter.{driver}.process");
            bool recognized = processFilter != "" ? ProcessFilterRegex().IsMatch(processFilter) : CleanFilterRegex().IsMatch(clean);

            return recognized && GetConfig($"filter.{driver}.required") == "true" &&
                   Regex.Split(attributes, "\r?\n").Any(line => line.EndsWith($" filter={driver}", StringComparison.Ordinal));
        });
        bool ignore = Regex.Split(Read(ConfigFile("core.excludesFile", "ignore")), "\r?\n").Contains(".env.keys*");

        return new ProtectState(filter, ignore, GetConfig("dotenvx.protect.configured") == "true" || filter || ignore);
    }

    public static void RemoveFilter()
    {
        string[] drivers = ["dotenvx", "dotenvx.protect"];
        foreach (string driver in drivers)
        {
            string processFilter = GetConfig($"filter.{driver}.process");
            if (processFilter != "" && !ProcessFilterRegex().IsMatch(processFilter))
            {
                throw new Exception($"Custom filter.{driver}.process found; remove it manually to disable protection.");
            }

            string clean = GetConfig($"filter.{driver}.clean");
            if (clean != "" && clean != "cat" && !CleanFilterRegex().IsMatch(clean))
            {
                throw new Exception($"Custom filter.{driver}.clean found; remove it manually to disable protection.");
            }
        }

        RemoveLines(ConfigFile("core.attributesFile", "attributes"),
            [.. drivers.SelectMany(driver => PrecommitEnvPatterns.Select(pattern => Attribute(pattern, driver)))]);
        // Keep any remaining or inherited attributes harmless, including custom patterns.
        foreach (string driver in drivers)
        {
            GlobalGit($"filter.{driver}.required", "false");
            GlobalGit($"filter.{driver}.process", "");
            GlobalGit($"filter.{driver}.clean", "cat");
        }
    }

    public static void RemoveIgnore()
    {
        string filename = ConfigFile("core.excludesFile", "ignore");
        if (!Regex.Split(Read(filename), "\r?\n").Contains(".env.keys*"))
        {
            return;
        }

        if (GetConfig("dotenvx.protect.ignoreFile", true) != filename)
        {
            throw new Exception(
                $"The existing .env.keys* rule in {filename} has no dotenvx ownership record. Remove it manually to disable it; other ignore rules will be preserved.");
        }

        RemoveLines(filename, [".env.keys*"]);
        GlobalGit("--unset-all", "dotenvx.protect.ignoreFile");
    }

    public static void MarkConfigured()
    {
        GlobalGit("dotenvx.protect.configured", "true");
    }

    // ---- removeLegacyProtectFilter -------------------------------------------------------------------------------------

    private static bool IsLegacy(string command)
    {
        string[] tokens = [.. Regex.Matches(command, "'[^']*'|\"[^\"]*\"|[^\\s'\";&|`$<>]+").Select(match => match.Value)];
        if (string.Join(' ', tokens) != command.Trim())
        {
            return false;
        }

        string[] args = [.. tokens.Select(token => Regex.IsMatch(token, "^['\"]") ? token[1..^1] : token)];
        if (string.Join(' ', args.TakeLast(3)) != "precommit --clean %f")
        {
            return false;
        }

        string[] executable = [.. args.SkipLast(3)];

        return (executable.Length == 1 && Path.GetFileName(executable[0]) == "dotenvx") ||
               (executable.Length == 2 && Path.GetFileName(executable[0]) is "node" or "node.exe" &&
                Path.GetFileName(executable[1]) == "dotenvx.js");
    }

    public static void RemoveLegacyProtectFilter(string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();
        string[] commands;
        try
        {
            commands = Git(["config", "--local", "--get-all", "filter.dotenvx.clean"], directory).Split('\n');
        }
        catch (GitCommandException error)
        {
            if (error.Status == 128)
            {
                return;
            }

            if (error.Status != 1)
            {
                throw;
            }

            commands = [];
        }

        // Only remove an entirely recognized legacy registration, never custom shell commands.
        if (!commands.All(IsLegacy))
        {
            return;
        }

        if (commands.Length > 0)
        {
            Git(["config", "--local", "--unset-all", "filter.dotenvx.clean"], directory);
            try
            {
                Git(["config", "--local", "--fixed-value", "--unset-all", "filter.dotenvx.required", "true"], directory);
            }
            catch (GitCommandException error) when (error.Status == 5)
            {
                // nothing to unset
            }
        }

        string attributesPath = Path.GetFullPath(Path.Combine(directory, Git(["rev-parse", "--git-path", "info/attributes"], directory)));
        if (!File.Exists(attributesPath) || new FileInfo(attributesPath).LinkTarget is not null)
        {
            return;
        }

        string[] oldRules = [.. PrecommitEnvPatterns.Select(pattern => Attribute(pattern, "dotenvx"))];
        string source = File.ReadAllText(attributesPath, Encoding.UTF8);
        string updated = string.Concat(SplitLines(source).Select(line =>
            oldRules.Contains(TrimEol(line)) ? line.Replace("filter=dotenvx", "filter=dotenvx.protect", StringComparison.Ordinal) : line));
        if (updated != source)
        {
            Fsx.WriteFileX(attributesPath, updated);
        }
    }

    // ---- uninstallPrecommitHook ----------------------------------------------------------------------------------------

    private const string LegacyScript =
        "#!/bin/sh\n\nif command -v dotenvx 2>&1 >/dev/null\nthen\n  dotenvx ext precommit\nelif npx dotenvx -V >/dev/null 2>&1\nthen\n  npx dotenvx ext precommit\nelse\n  echo \"[dotenvx][precommit] 'dotenvx' command not found\"\n  echo \"[dotenvx][precommit] ? install it with [curl -fsS https://dotenvx.sh | sh]\"\n  echo \"[dotenvx][precommit] ? other install options [https://dotenvx.com/docs/install]\"\n  exit 1\nfi\n";

    private static bool Within(string directory, string file)
    {
        string relative = Path.GetRelativePath(directory, file);

        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    public sealed record UninstallResult(bool Removed, string? Warning = null);

    [GeneratedRegex(@"dotenvx\s+(?:ext\s+)?precommit")]
    private static partial Regex PrecommitMentionRegex();

    public static UninstallResult UninstallPrecommitHook(string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();
        string root;
        try
        {
            root = Git(["rev-parse", "--show-toplevel"], directory);
        }
        catch (GitCommandException error) when (error.Status == 128)
        {
            return new UninstallResult(false);
        }

        string hookPath = Path.GetFullPath(Path.Combine(directory, Git(["rev-parse", "--git-path", "hooks/pre-commit"], directory)));
        string commonDir = Path.GetFullPath(Path.Combine(directory, Git(["rev-parse", "--git-common-dir"], directory)));
        UninstallResult Manual()
        {
            return new UninstallResult(false,
                $"pre-commit hook left unchanged; remove its dotenvx precommit entry manually [{hookPath}]");
        }

        FileInfo stat = new(hookPath);
        if (!stat.Exists)
        {
            return new UninstallResult(false);
        }

        // Never modify symlinked hooks or shared hooks outside this repository.
        string real = stat.ResolveLinkTarget(true)?.FullName ?? stat.FullName;
        if (stat.LinkTarget is not null || (!Within(root, real) && !Within(commonDir, real)))
        {
            return Manual();
        }

        string source = File.ReadAllText(hookPath, Encoding.UTF8);
        string hook = InstallPrecommitHook.HookScript;
        string[] templates =
        [
            hook, LegacyScript, hook.Replace("dotenvx precommit", "dotenvx ext precommit", StringComparison.Ordinal),
            LegacyScript.Replace("dotenvx ext precommit", "dotenvx precommit", StringComparison.Ordinal)
        ];
        string next = source;
        foreach (string template in templates)
        {
            foreach (string block in new[] { template, template.Replace("\n", "\r\n", StringComparison.Ordinal) })
            {
                // Match whole, known installer blocks; do not guess at shell structure.
                next = Regex.Replace(next, "(^|\\n)" + Regex.Escape(block), "$1");
            }
        }

        if (next == source)
        {
            return PrecommitMentionRegex().IsMatch(source) ? Manual() : new UninstallResult(false);
        }

        if (next.Trim() == "")
        {
            File.Delete(hookPath);
        }
        else
        {
            Fsx.WriteFileX(hookPath, next);
        }

        return new UninstallResult(true, PrecommitMentionRegex().IsMatch(next) ? Manual().Warning : null);
    }

    // ---- plaintextKeys / protectEnvfile --------------------------------------------------------------------------------

    public static List<string> PlaintextKeys(string content)
    {
        return [.. Primitives.Primitives.Scan(content, new ScanOptions()).Parsed
            .Where(pair => !pair.Key.StartsWith("DOTENV_PUBLIC_KEY", StringComparison.Ordinal) && pair.Value.Any(value =>
                value.Trim() != "" && !Primitives.Primitives.Encrypted(value) && !value.StartsWith("op://", StringComparison.Ordinal) &&
                !value.StartsWith("bw://", StringComparison.Ordinal)))
            .Select(pair => pair.Key)];
    }

    private static EnvfileSchema? ReadPolicy(string policyPath, string file)
    {
        EnvfileSchema schema = ReadEnvfile.Read(policyPath, [file]);
        if (!schema.Exists)
        {
            // A dangling policy symlink is a broken policy, not an absent policy.
            FileInfo info = new(policyPath);
            if (info.Exists || info.LinkTarget is not null)
            {
                throw new Exception($"Cannot read Envfile: {policyPath}");
            }

            return null;
        }

        return schema;
    }

    /// <summary>protectEnvfile.js: the plaintext keys Git's incoming blob must not contain, or null when no Envfile applies.</summary>
    public static List<string>? ProtectEnvfile(string filepath, string content)
    {
        string file = Path.GetFullPath(filepath);
        string directory = Path.GetDirectoryName(file)!;
        EnvfileSchema? schema = ReadPolicy(Path.GetFullPath(Path.Combine(directory, "Envfile")), file);
        if (schema is null)
        {
            string root;
            try
            {
                root = Git(["rev-parse", "--show-toplevel"], directory);
            }
            catch (Exception)
            {
                // Outside a Git working tree, only an adjacent policy can apply.
                return null;
            }

            if (Path.GetFullPath(root) != directory)
            {
                schema = ReadPolicy(Path.GetFullPath(Path.Combine(root, "Envfile")), file);
            }
        }

        if (schema is null)
        {
            return null;
        }

        return [.. PlaintextKeys(content).Where(key =>
            Regex.IsMatch(key, "^DOTENV_PRIVATE_KEY(?:_|$)") || !schema.Keys.TryGetValue(key, out KeyPolicy? policy) || policy.Encrypted)];
    }
}
