namespace Dotenvx.Core.Interop.Dotenvx.Types;

public class ProcessedEnv
{
    public EnvType Type { get; set; }
    public string Filepath { get; set; } = string.Empty;
    public string String { get; set; } = string.Empty;
    public IDictionary<string, string> Parsed { get; set; } = new Dictionary<string, string>();
    public IList<Exception> Errors { get; set; } = new List<Exception>();
    public IDictionary<string, string> Injected { get; set; } = new Dictionary<string, string>();
    public IDictionary<string, string> PreExisted { get; set; } = new Dictionary<string, string>();
    public string PrivateKeyName { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Src { get; set; } = string.Empty;
}