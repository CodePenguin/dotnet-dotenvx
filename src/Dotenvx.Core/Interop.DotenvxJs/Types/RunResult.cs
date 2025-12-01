namespace Dotenvx.Interop.DotenvxJs.Types;

public class RunResult
{
    public required RunProcessedEnv[] ProcessedEnvs { get; init; }
    public required string[] ReadableStrings { get; init; }
    public required string[] ReadableFilePaths { get; init; }
    public required string[] UniqueInjectedKeys { get; init; }
    public required IReadOnlyDictionary<string, string> BeforeEnv { get; init; }
    public required IReadOnlyDictionary<string, string> AfterEnv { get; init; }
}