namespace Dotenvx.Interop.DotenvxJs.Types;

public class GetResult
{
    public required IReadOnlyDictionary<string, string> Parsed { get; init; }
    public required Exception[] Errors { get; init; }
}