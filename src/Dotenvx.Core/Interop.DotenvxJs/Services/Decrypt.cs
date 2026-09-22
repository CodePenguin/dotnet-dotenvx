using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/decrypt.js</remarks>
public class Decrypt(EnvItem[] envs, string[]? keys, string[]? excludeKeys, string? envKeysFilePath)
{
    private readonly HashSet<string> _changedFilePaths = [];

    private readonly EnvItem[] _envs = DetermineEnvs(envs, Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

    private readonly List<ProcessedDecrypt> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];

    private Func<string, bool> _exclude = _ => false;
    private Func<string, bool> _include = _ => true;

    public DecryptResult Run()
    {
        _exclude = Picomatch(excludeKeys);
        _include = Picomatch(keys);

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
            string envSrc = Fsx.ReadFileX(filePath, encoding);
            Dictionary<string, object> envParsed = DotenvParse(envSrc);

            string? privateKey = FindPrivateKey(envFilePath, envKeysFilePath);
            string privateKeyName = GuessPrivateKeyName(envFilePath);

            row.PrivateKey = privateKey;
            row.PrivateKeyName = privateKeyName;
            row.Changed = false;

            foreach ((string envKey, object envValue) in envParsed)
            {
                string value = envValue as string ?? "";

                if (_exclude(envKey))
                {
                    continue;
                }

                if (keys?.Length > 0 && !_include(envKey))
                {
                    continue;
                }

                bool encrypted = IsEncrypted(value);
                if (!encrypted)
                {
                    continue;
                }

                try
                {
                    string decryptedValue = DecryptKeyValue(envKey, value, privateKeyName, privateKey);
                    envSrc = Replace(envSrc, envKey, decryptedValue);
                    row.Keys.Add(envKey);
                    row.Changed = true;
                }
                catch (Exception)
                {
                    // Matches @dotenvx/primitives' parsearrays() (used only by the `decrypt` command,
                    // confirmed via live oracle - unlike run/get/set's singular parse(), which surfaces
                    // the specific WRONG_PRIVATE_KEY/MISSING_PRIVATE_KEY/etc. cause): one key's
                    // decryption failure doesn't abort the rest of the file, and the specific
                    // underlying cause is collapsed into a single generic message per failed key.
                    row.Error ??= new DecryptionFailedException($"could not decrypt {envKey}");
                }
            }

            row.EnvSrc = envSrc;
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