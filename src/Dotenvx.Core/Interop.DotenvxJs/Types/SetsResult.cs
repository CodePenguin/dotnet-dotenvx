namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class SetsResult
{
    public required IList<ProcessedSet> ProcessedEnvs { get; set; }
    public required IList<string> ChangedFilePaths { get; set; }
    public required IList<string> UnchangedFilePaths { get; set; }
}