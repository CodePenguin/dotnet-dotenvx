namespace Dotenvx.Interop.DotenvxJs.Types;

internal class EncryptResult
{
    public string? KeysSrc { get; init; }
    public EncryptProcessedEnv[] ProcessedEnvs { get; init; } = [];
    public string[] ChangedFilePaths { get; init; } = [];
    public string[] UnchangedFilePaths { get; init; } = [];
}
