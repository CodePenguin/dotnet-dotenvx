namespace Dotenvx.Interop.DotenvxJs.Types;

public class DelResult
{
    public required ProcessedDel[] ProcessedEnvs { get; init; }
    public required string[] ChangedFilePaths { get; init; }
    public required string[] UnchangedFilePaths { get; init; }
}
