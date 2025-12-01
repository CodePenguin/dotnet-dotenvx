namespace Dotenvx.Interop.DotenvxJs.Types;

public class RotateProcessedEnv
{
    public required string[] Keys { get; set; }
    public required EnvType Type { get; set; }
    public required string FilePath { get; set; }
    public required string EnvFilePath { get; set; }
    public Exception? Error { get; set; }
    public bool Changed { get; set; }
    public required string EnvSrc { get; set; }
    public bool PrivateKeyAdded { get; set; }
    public string? PrivateKeyName { get; set; }
    public string? PrivateKey { get; set; }
    public required string EnvKeysSrc { get; set; }
    public required string EnvKeysFilePath { get; set; }
}