namespace Dotenvx.Interop.DotenvxJs.Types;

public class RunProcessedEnv
{
    public EnvType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string String { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Parsed { get; set; } = new Dictionary<string, string>();
    public IList<Exception> Errors { get; set; } = [];
    public IReadOnlyDictionary<string, string> Injected { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> PreExisted { get; set; } = new Dictionary<string, string>();
    public string? PrivateKeyName { get; set; } = string.Empty;
    public string? PrivateKey { get; set; } = string.Empty;
    public string Src { get; set; } = string.Empty;
}