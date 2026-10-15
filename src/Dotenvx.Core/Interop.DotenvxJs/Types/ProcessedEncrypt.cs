namespace Dotenvx.Interop.DotenvxJs.Types;

public class EncryptProcessedEnv
{
    public IList<string> Keys { get; init; } = [];
    public EnvType Type { get; init; }
    public string FilePath { get; set; } = string.Empty;
    public string EnvFilePath { get; set; } = string.Empty;
    public Exception? Error { get; set; }
    public bool Changed { get; set; }
    public string EnvSrc { get; set; } = string.Empty;
    public string? PublicKey { get; set; }

    /// <summary>`row.keySources`: where the private key of the file lives (new keys: the custodian chosen).</summary>
    public IReadOnlyDictionary<string, string>? KeySources { get; set; }

    /// <summary>`row.nativePrivateKeyAdded`: the private key was added to the OS secret store.</summary>
    public bool NativePrivateKeyAdded { get; set; }
}