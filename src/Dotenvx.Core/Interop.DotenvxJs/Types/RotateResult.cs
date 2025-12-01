namespace Dotenvx.Interop.DotenvxJs.Types;

public class RotateResult
{
    public required RotateProcessedEnv[] ProcessedEnvs { get; init; }
    public required string[] ChangedFilePaths { get; init; }
    public required string[] UnchangedFilePaths { get; init; }
}