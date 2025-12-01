namespace Dotenvx.Interop.DotenvxJs.Types;

public class ParseResult
{
    public required IReadOnlyDictionary<string, string> Parsed { get; init; }
    public required IReadOnlyDictionary<string, string> ProcessEnv { get; init; }
    public required IReadOnlyDictionary<string, string> Injected { get; init; }
    public required IReadOnlyDictionary<string, string> PreExisted { get; init; }
    public required Exception[] Errors { get; init; }
}