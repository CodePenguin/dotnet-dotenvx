using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/logSetSummary.js. The closing line of `set`: the files that changed (or, when none
/// did, the unchanged ones), with the public keys and the custody the keys came from when the value was encrypted.
/// </remarks>
internal static class LogSetSummary
{
    public static void Log(IDotenvxLogger? logger, string key, bool encrypt, IEnumerable<ProcessedSet> processedEnvs,
        IReadOnlyList<string> changedFilePaths, IReadOnlyList<string> unchangedFilePaths)
    {
        Dictionary<string, InjectSummaryFile> rows = [];
        foreach (ProcessedSet row in processedEnvs)
        {
            rows[row.EnvFilePath] = new InjectSummaryFile($"({row.EnvFilePath})", PublicKeys(row.EnvSrc ?? ""), row.KeySources);
        }

        IReadOnlyList<string> paths = changedFilePaths.Count > 0 ? changedFilePaths : unchangedFilePaths;
        if (paths.Count == 0)
        {
            return;
        }

        InjectSummarySources summary = encrypt
            ? InjectSummarySourcesOf(paths.Select(path => rows.GetValueOrDefault(path) ?? new InjectSummaryFile($"({path})")))
            : new InjectSummarySources([$"({string.Join(',', paths)})"], "");
        string files = string.Join(',', summary.Files);
        if (changedFilePaths.Count > 0)
        {
            logger?.Success($"{(encrypt ? "◈ encrypted" : "◇ set")} {key} {files}{summary.Suffix}");
        }
        else
        {
            logger?.Info($"○ no change {files}{summary.Suffix}");
        }
    }
}
