namespace Dotenvx.Interop.DotenvxJs.Types;

internal class GetResult
{
    public required IReadOnlyDictionary<string, string> Parsed { get; init; }
    public required Exception[] Errors { get; init; }
}
