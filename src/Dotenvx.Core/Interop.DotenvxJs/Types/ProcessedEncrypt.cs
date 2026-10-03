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
}