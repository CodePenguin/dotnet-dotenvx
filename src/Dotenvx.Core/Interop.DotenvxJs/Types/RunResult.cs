namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class RunResult
{
    public required IList<ProcessedEnv> ProcessedEnvs { get; set; }
    public required IList<string> ReadableStrings { get; set; }
    public required IList<string> ReadableFilepaths { get; set; }
    public required IList<string> UniqueInjectedKeys { get; set; }
    public required IDictionary<string, string> BeforeEnv { get; set; }
    public required IDictionary<string, string> AfterEnv { get; set; }
}