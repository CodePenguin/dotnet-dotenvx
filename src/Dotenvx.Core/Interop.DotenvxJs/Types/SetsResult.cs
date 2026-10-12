namespace Dotenvx.Interop.DotenvxJs.Types;

public class SetsResult
{
    public string? KeysSrc { get; init; }
    public required ProcessedSet[] ProcessedEnvs { get; init; }
    public required string[] ChangedFilePaths { get; init; }
    public required string[] UnchangedFilePaths { get; init; }
}