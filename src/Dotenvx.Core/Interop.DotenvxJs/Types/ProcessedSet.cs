namespace Dotenvx;

public class ProcessedSet
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public EnvType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string EnvFilePath { get; set; } = string.Empty;
    public bool Changed { get; set; }
    public bool PrivateKeyAdded { get; set; }
    public bool NativePrivateKeyAdded { get; set; }
    public string EnvSrc { get; set; } = string.Empty;
    public Exception? Error { get; set; }

    /// <summary>Where each public key's private key is kept (`row.keySources`), for the summary line.</summary>
    public IReadOnlyDictionary<string, string>? KeySources { get; set; }
}
