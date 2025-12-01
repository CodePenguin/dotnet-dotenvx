namespace Dotenvx.Interop.DotenvxJs.Types;

public class ProcessedSet
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public EnvType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string EnvFilePath { get; set; } = string.Empty;
    public bool Changed { get; set; }
    public string? OriginalValue { get; set; }
    public string? EncryptedValue { get; set; }
    public string? PublicKey { get; set; }
    public string? PrivateKey { get; set; }
    public string? PrivateKeyName { get; set; }
    public bool PrivateKeyAdded { get; set; }
    public string? EnvKeysFilePath { get; set; }
    public string EnvSrc { get; set; } = string.Empty;
    public Exception? Error { get; set; }
}