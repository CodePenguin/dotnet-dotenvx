namespace Dotenvx.Primitives;

/// <summary>A file in an injection summary: public metadata only (never private keys or values).</summary>
public sealed record InjectSummaryFile(string Filepath, IReadOnlyList<string>? PublicKeys = null,
    IReadOnlyDictionary<string, string>? KeySources = null);

/// <summary>The labels and via-suffix shared by the injection, encryption and decryption summaries.</summary>
public sealed record InjectSummarySources(List<string> Files, string Suffix);

/// <remarks>
/// Counterpart of the @dotenvx/primitives `injectsummary` export: `⟐ injected env (N) from FILES via SOURCES` without logging
/// or colors. A file is its path, plus ` (ABC DEF)` for the first six hex digits of each public key when it has any; each
/// key's source names a custody label.
/// </remarks>
public static partial class Primitives
{
    private static string NativeSourceLabel()
    {
        if (OperatingSystem.IsMacOS())
        {
            return "Keychain ⛉";
        }

        if (OperatingSystem.IsWindows())
        {
            return "Credential Manager ⛉";
        }

        return OperatingSystem.IsLinux() ? "Secret Service ⛉" : "Local ⛉";
    }

    private static string? SourceLabel(string source)
    {
        return source switch
        {
            "native" => NativeSourceLabel(),
            "macosKeychain" => "Keychain ⛉",
            "windowsCredentialManager" => "Credential Manager ⛉",
            "linuxSecretService" => "Secret Service ⛉",
            "file" or ".env.keys" => ".env.keys ⛉",
            "environment" => "Env ⛉",
            "onepassword" => "1Password ⛊",
            "bitwarden" => "Bitwarden ⛊",
            "armor" or "armored" => "Armor ⛨",
            "enclaved" => "Armor Enclave ◊",
            _ => null
        };
    }

    private static string ShortPublicKey(string publicKey)
    {
        string head = publicKey.Length > 6 ? publicKey[..6] : publicKey;
        head = head.ToUpperInvariant();
        return head.Length > 3 ? $"{head[..3]} {head[3..]}" : head;
    }

    /// <summary>The file labels and the ` via ...` suffix of a summary; a plain path is a file without metadata.</summary>
    public static InjectSummarySources InjectSummarySourcesOf(IEnumerable<InjectSummaryFile> files)
    {
        List<string> labels = [];
        HashSet<string> sources = [];
        List<string> order = [];
        foreach (InjectSummaryFile file in files)
        {
            IReadOnlyList<string> publicKeys = file.PublicKeys ?? [];
            foreach (string publicKey in publicKeys)
            {
                string? source = null;
                file.KeySources?.TryGetValue(publicKey, out source);
                string label = (source != null ? SourceLabel(source) : null) is { Length: > 0 } known
                    ? known
                    : !string.IsNullOrEmpty(source) ? source : "source unknown";
                if (sources.Add(label))
                {
                    order.Add(label);
                }
            }

            labels.Add(publicKeys.Count > 0
                ? $"{file.Filepath} ({string.Join("+", publicKeys.Select(ShortPublicKey))})"
                : file.Filepath);
        }

        return new InjectSummarySources(labels, order.Count > 0 ? $" via {string.Join(",", order)}" : "");
    }

    public static string InjectSummary(int count, IEnumerable<InjectSummaryFile>? files = null)
    {
        InjectSummarySources sources = InjectSummarySourcesOf(files ?? []);
        string joined = string.Join(", ", sources.Files);
        return $"⟐ injected env ({count}){(joined.Length > 0 ? $" from {joined}" : "")}{sources.Suffix}";
    }
}
