using System.Collections;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/del.js. Upstream delegates the actual key removal to
/// the external @dotenvx/primitives package (not vendored in this submodule); the removal regex here
/// was derived empirically by diffing real dotenvx CLI output for a range of .env fixtures (quoted,
/// multiline, exported, and commented values; first/middle/last key; with and without a trailing
/// newline) rather than ported from readable source.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Del
{
    private static readonly Func<string, EnvItem[], Del> DefaultFactory = (key, envs) => new Del(key, envs);

    private readonly EnvItem[] _envs;
    private readonly string _key;

    public Del(string key, EnvItem[] envs)
    {
        _key = key;

        Dictionary<string, string> processEnv = new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        _envs = DetermineEnvs(envs, processEnv);
    }

    protected Del() : this(string.Empty, [])
    {
        // For unit tests
    }

    public static Func<string, EnvItem[], Del> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual DelResult Run()
    {
        List<ProcessedDel> processedEnvs = [];
        List<string> changedFilePaths = [];
        List<string> unchangedFilePaths = [];

        foreach (EnvItem env in _envs)
        {
            if (env.Type != EnvType.EnvFile)
            {
                continue;
            }

            string envFilePath = env.Value;
            string filePath = Path.GetFullPath(envFilePath);
            ProcessedDel row = new() { Key = _key, Type = EnvType.EnvFile, FilePath = filePath, EnvFilePath = envFilePath };

            try
            {
                string encoding = DetectEncoding(filePath);
                string before = Fsx.ReadFileX(filePath, encoding);
                string after = Remove(before, _key);

                row.EnvSrc = after;
                if (after != before)
                {
                    row.Changed = true;
                    changedFilePaths.Add(envFilePath);
                }
                else
                {
                    unchangedFilePaths.Add(envFilePath);
                }
            }
            catch (Exception e)
            {
                row.Error = e is FileNotFoundException or DirectoryNotFoundException
                    ? new MissingEnvFileException(envFilePath)
                    : e;
            }

            processedEnvs.Add(row);
        }

        return new DelResult
        {
            ProcessedEnvs = processedEnvs.ToArray(),
            ChangedFilePaths = changedFilePaths.ToArray(),
            UnchangedFilePaths = unchangedFilePaths.ToArray()
        };
    }
}
