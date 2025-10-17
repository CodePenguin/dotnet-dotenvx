namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class ParseResult()
{
    public required IDictionary<string, string> Parsed { get; init; }
    public required IDictionary<string, string> ProcessEnv { get; init; }
    public required IDictionary<string, string> Injected { get; init; }
    public required IDictionary<string, string> PreExisted { get; init; }
    public required IList<Exception> Errors { get; init; }
}