using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/decrypt.js. Like the original, this parses with primitives'
/// `parsearrays` (keyring from `fk`, default `.env.keys` in the working directory, plus the process
/// environment; `ik`/`ek` choose which keys to decrypt) and upserts every parsed value that differs from
/// the source, so a failed key leaves its ciphertext and reports one combined DECRYPTION_FAILED error
/// without stopping the rest of the file.
/// </remarks>
public class Decrypt(EnvItem[] envs, string[]? keys, string[]? excludeKeys, string? envKeysFilePath)
{
    private readonly HashSet<string> _changedFilePaths = [];

    private readonly EnvItem[] _envs = DetermineEnvs(envs, Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

    private readonly List<ProcessedDecrypt> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];

    public DecryptResult Run()
    {
        foreach (EnvItem env in _envs)
        {
            if (env.Type == EnvType.EnvFile)
            {
                DecryptEnvFile(env.Value);
            }
        }

        return new DecryptResult
        {
            ProcessedEnvs = _processedEnvs.ToArray(),
            ChangedFilePaths = _changedFilePaths.ToArray(),
            UnchangedFilePaths = _unchangedFilePaths.ToArray()
        };
    }

    private void DecryptEnvFile(string envFilePath)
    {
        ProcessedDecrypt row = new() { Keys = [], Type = EnvType.EnvFile };

        string filePath = Path.GetFullPath(envFilePath);
        row.FilePath = filePath;
        row.EnvFilePath = envFilePath;

        try
        {
            string encoding = DetectEncoding(filePath);
            row.EnvSrc = Fsx.ReadFileX(filePath, encoding);

            PrimitivesParseArraysOutput result = ParseArraysSync(row.EnvSrc, new PrimitivesParseOptions
            {
                Ik = keys,
                Ek = excludeKeys,
                Fk = string.IsNullOrEmpty(envKeysFilePath) ? null : [envKeysFilePath],
                ReadFile = path => Fsx.ReadFileX(path)
            });

            if (result.Errors.Count > 0)
            {
                row.Error = CustomError(result.Errors[0]);
            }

            foreach ((string key, List<string> values) in result.Parsed)
            {
                string before = row.EnvSrc;
                string after = Upsert(before, key, values.ToArray());
                if (after == before)
                {
                    continue;
                }

                row.EnvSrc = after;
                row.Changed = true;
                row.Keys.Add(key);
            }

            if (row.Changed)
            {
                _changedFilePaths.Add(envFilePath);
            }
            else
            {
                _unchangedFilePaths.Add(envFilePath);
            }
        }
        catch (Exception e)
        {
            row.Error = e is FileNotFoundException ? new MissingEnvFileException(envFilePath) : e;
        }

        _processedEnvs.Add(row);
    }
}
