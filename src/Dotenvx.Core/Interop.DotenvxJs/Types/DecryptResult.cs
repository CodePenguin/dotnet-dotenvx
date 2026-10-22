namespace Dotenvx.Interop.DotenvxJs.Types;

internal class DecryptResult
{
    public ProcessedDecrypt[] ProcessedEnvs { get; init; } = [];
    public string[] ChangedFilePaths { get; init; } = [];
    public string[] UnchangedFilePaths { get; init; } = [];
}
