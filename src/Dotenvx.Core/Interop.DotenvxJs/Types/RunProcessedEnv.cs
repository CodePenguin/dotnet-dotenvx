namespace Dotenvx.Interop.DotenvxJs.Types;

internal class RunProcessedEnv
{
    public EnvType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string String { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Parsed { get; set; } = new Dictionary<string, string>();
    public IList<Exception> Errors { get; set; } = [];
    public IReadOnlyDictionary<string, string> Injected { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> PreExisted { get; set; } = new Dictionary<string, string>();
    public string Src { get; set; } = string.Empty;

    /// <summary>Where each private key came from (`row.keySources`): environment, .env.keys, custom, armor or a provider name.</summary>
    public IReadOnlyDictionary<string, string>? KeySources { get; set; }
}
