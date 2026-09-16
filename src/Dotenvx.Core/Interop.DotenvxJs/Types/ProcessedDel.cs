namespace Dotenvx.Interop.DotenvxJs.Types;

public class ProcessedDel
{
    public string Key { get; set; } = string.Empty;
    public EnvType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string EnvFilePath { get; set; } = string.Empty;
    public bool Changed { get; set; }
    public string EnvSrc { get; set; } = string.Empty;
    public Exception? Error { get; set; }
}
