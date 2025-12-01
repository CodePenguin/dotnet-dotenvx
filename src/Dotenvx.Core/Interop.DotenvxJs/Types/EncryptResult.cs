namespace Dotenvx.Interop.DotenvxJs.Types;

public class EncryptResult
{
    public EncryptProcessedEnv[] ProcessedEnvs { get; init; } = [];
    public string[] ChangedFilePaths { get; init; } = [];
    public string[] UnchangedFilePaths { get; init; } = [];
}