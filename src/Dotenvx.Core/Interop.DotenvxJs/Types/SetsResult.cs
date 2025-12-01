namespace Dotenvx.Interop.DotenvxJs.Types;

public class SetsResult
{
    public required ProcessedSet[] ProcessedEnvs { get; init; }
    public required string[] ChangedFilePaths { get; init; }
    public required string[] UnchangedFilePaths { get; init; }
}