namespace Dotenvx.Interop.DotenvxJs.Types;

public class OpsPayload
{
    public IReadOnlyDictionary<string, string> BeforeEnv { get; init; } = new Dictionary<string, string>();
    public RunProcessedEnv[] ProcessedEnvs { get; init; } = [];
    public IReadOnlyDictionary<string, string> AfterEnv { get; init; } = new Dictionary<string, string>();
}