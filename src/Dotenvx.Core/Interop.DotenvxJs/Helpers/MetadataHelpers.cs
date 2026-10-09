using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/sanitizeCommandForMetadata.js</remarks>
public static partial class SanitizeCommandForMetadata
{
    [GeneratedRegex("\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"|'([^'\\\\]*(?:\\\\.[^'\\\\]*)*)'|(\\S+)")]
    private static partial Regex PartsRegex();

    private static List<string> CommandParts(string command)
    {
        // `match[1] || match[2] || match[3]`: the first non-empty group
        return [.. PartsRegex().Matches(command).Select(match =>
            match.Groups[1].Value != "" ? match.Groups[1].Value
            : match.Groups[2].Value != "" ? match.Groups[2].Value
            : match.Groups[3].Value)];
    }

    private static string? FlagName(string part)
    {
        return part.StartsWith("--") ? part[2..].Split('=')[0].ToLowerInvariant() : null;
    }

    private static bool IsSecretFlag(string part)
    {
        string? name = FlagName(part);
        if (name is null)
        {
            return false;
        }

        return name is "pass" or "password" or "passphrase" or "pwd" || name.Contains("token") ||
               name.Contains("password") || name.Contains("secret") || name.Contains("api-key") ||
               name.Contains("apikey") || name.Contains("private-key");
    }

    public static string Sanitize(string command)
    {
        return Sanitize(CommandParts(command));
    }

    public static string Sanitize(IEnumerable<string> command)
    {
        List<string> parts = [.. command];
        var sanitized = new List<string>();

        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];

            if (IsSecretFlag(part) && part.Contains('='))
            {
                sanitized.Add($"{part.Split('=')[0]}=[REDACTED]");
                continue;
            }

            if (IsSecretFlag(part))
            {
                sanitized.Add(part);
                if (i + 1 < parts.Count)
                {
                    sanitized.Add("[REDACTED]");
                    i++;
                }

                continue;
            }

            sanitized.Add(part);
        }

        return string.Join(" ", sanitized);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/keypairMetadata.js</remarks>
public static class KeypairMetadata
{
    private static string? GitRoot()
    {
        try
        {
            SecretCliResult result = SecretCli.Exec("git", ["rev-parse", "--show-toplevel"], null);

            return result is { NotFound: false, ExitCode: 0 } ? result.Stdout.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeFilepath(string envFile)
    {
        string? root = GitRoot();
        string filepath = NodePath.Resolve(envFile);

        return Path.GetRelativePath(root ?? Directory.GetCurrentDirectory(), filepath).Replace('\\', '/');
    }

    private static string ProjectName()
    {
        return Path.GetFileName((GitRoot() ?? Directory.GetCurrentDirectory()).TrimEnd('/', '\\'));
    }

    private static string[]? EnvKeys(string envFile)
    {
        byte[] bytes;
        try
        {
            bytes = Fsx.ReadFileSync(envFile);
        }
        catch
        {
            return null;
        }

        var parsed = Primitives.Primitives.Scan(Encoding.UTF8.GetString(bytes), new Primitives.ScanOptions()).Parsed;

        return parsed.Count == 0 ? null : [.. parsed.Keys];
    }

    private static string? CommandFromMetadata(string? metadata)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(metadata ?? "");
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("command", out JsonElement command) &&
                command.ValueKind == JsonValueKind.String && command.GetString() != "")
            {
                return SanitizeCommandForMetadata.Sanitize(command.GetString()!);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The metadata sent with a keypair (empty values are left out, like upstream's `compact`).</summary>
    public static Dictionary<string, object> Get(string envFile = ".env", string? metadata = null)
    {
        var result = new Dictionary<string, object>();

        void Add(string key, object? value)
        {
            if (value is not null && value as string != "")
            {
                result[key] = value;
            }
        }

        Add("filepath", NormalizeFilepath(envFile));
        Add("filename", CanonicalEnvFilename(envFile));
        Add("environment", GuessEnvironment(envFile));
        Add("project_name", ProjectName());
        Add("keys", EnvKeys(envFile));
        Add("command", CommandFromMetadata(metadata));

        return result;
    }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/removeDynamicHelpSection.js and removeOptionsHelpParts.js - post-processors
/// for commander's help text. They are ported (and tested) as pure line functions; this port's help text comes from
/// System.CommandLine, so they are not applied to it.
/// </remarks>
public static partial class HelpTextFilters
{
    /// <summary>Removes the "Arguments:" block (that line and the 3 after it) from the help lines.</summary>
    public static List<string> RemoveDynamicHelpSection(List<string> lines)
    {
        int argumentsHelpIndex = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] == "Arguments:")
            {
                argumentsHelpIndex = i;
                break;
            }
        }

        // JS: `if (argumentsHelpIndex)` - index 0 counts as "not found" too
        if (argumentsHelpIndex != 0)
        {
            lines.RemoveRange(argumentsHelpIndex, Math.Min(4, lines.Count - argumentsHelpIndex));
        }

        return lines;
    }

    [GeneratedRegex(@"^(\s{2})(\S(?:.*\S)?)\s{2,}(\S.*)$")]
    private static partial Regex CommandLine();

    private static void RemoveOptionsSection(List<string> lines)
    {
        int start = lines.IndexOf("Options:");
        if (start == -1)
        {
            return;
        }

        int end = start + 1;
        while (end < lines.Count && lines[end] != "" && !lines[end].EndsWith(':'))
        {
            end++;
        }

        // drop trailing blank line after the Options block when present
        if (end < lines.Count && lines[end] == "")
        {
            end++;
        }

        lines.RemoveRange(start, end - start);
    }

    /// <summary>Removes ` [options]` from usage/command lines, hides the Options section and re-aligns the command descriptions.</summary>
    public static List<string> RemoveOptionsHelpParts(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            int index = lines[i].IndexOf(" [options]", StringComparison.Ordinal);
            if (index >= 0)
            {
                lines[i] = lines[i].Remove(index, " [options]".Length);
            }
        }

        RemoveOptionsSection(lines);

        int commandsStart = lines.IndexOf("Commands:");
        if (commandsStart != -1)
        {
            commandsStart++;
            var commands = new List<(int Index, string Indent, string Command, string Description)>();

            for (int i = commandsStart; i < lines.Count; i++)
            {
                string line = lines[i];
                if (line == "" || line.EndsWith(':'))
                {
                    break;
                }

                Match match = CommandLine().Match(line);
                if (match.Success)
                {
                    commands.Add((i, match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
                }
            }

            if (commands.Count > 0)
            {
                int max = commands.Max(item => item.Command.Length);
                foreach ((int index, string indent, string command, string description) in commands)
                {
                    lines[index] = $"{indent}{command.PadRight(max + 2)}{description}";
                }
            }
        }

        return lines;
    }
}
