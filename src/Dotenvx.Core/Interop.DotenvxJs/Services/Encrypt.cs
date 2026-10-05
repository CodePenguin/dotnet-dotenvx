using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/encrypt.js. The public key is `publickeys(src)[0]` (the
/// first DOTENV_PUBLIC_KEY* in the file, whatever its name); when there is none a fresh keypair is made, the
/// public key header is prepended to the env source and the private key is appended to the returned
/// `KeysSrc` (the keys file is read and written relative to the working directory, default `.env.keys`;
/// callers write it). Values are chosen with `scan` (`ik`/`ek`), encrypted unless already encrypted, and
/// upserted per occurrence; DOTENV_PUBLIC_KEY* and *_PLAIN keys are never encrypted.
/// </remarks>
public class Encrypt(EnvItem[] envs, string[]? keys, string[]? excludeKeys, string? envKeysFilePath,
    bool noCreate = false)
{
    private const string SampleEnvKit = "HELLO=\"World\"\n";

    private readonly HashSet<string> _changedFilePaths = [];

    private readonly EnvItem[] _envs = DetermineEnvs(envs, Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

    private readonly List<EncryptProcessedEnv> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];

    private string? _keysSrc;

    /// <summary>
    /// `noArmor`: true keeps new keys in .env.keys. The CLI always sets it from `--no-armor` and the Armor session; the
    /// default is "no Armor" so library callers never reach the network or a prompt unasked.
    /// </summary>
    public bool NoArmor { get; set; } = true;

    private bool _noArmor;

    public EncryptResult Run()
    {
        string fk = string.IsNullOrEmpty(envKeysFilePath) ? ".env.keys" : envKeysFilePath;
        _noArmor = NoArmor; // key storage selector below may change it

        // set up keysSrc
        string fkPath = Path.GetFullPath(fk);
        if (Fsx.ExistsSync(fkPath))
        {
            try
            {
                _keysSrc = Fsx.ReadFileX(fkPath, DetectEncoding(fkPath));
            }
            catch (UnauthorizedAccessException)
            {
                // do nothing (scenario: chmod a-r .env.keys)
            }
        }

        foreach (EnvItem env in _envs)
        {
            if (env.Type == EnvType.EnvFile)
            {
                EncryptEnvFile(env.Value, fk);
            }
        }

        return new EncryptResult
        {
            KeysSrc = _keysSrc,
            ProcessedEnvs = _processedEnvs.ToArray(),
            ChangedFilePaths = _changedFilePaths.ToArray(),
            UnchangedFilePaths = _unchangedFilePaths.ToArray()
        };
    }

    private void EncryptEnvFile(string envFilePath, string fk)
    {
        EncryptProcessedEnv row = new() { Keys = [], Type = EnvType.EnvFile };

        string filepath = Path.GetFullPath(envFilePath);
        row.FilePath = filepath;
        row.EnvFilePath = envFilePath;

        try
        {
            string envSrc;
            if (!Fsx.ExistsSync(filepath) && !noCreate)
            {
                envSrc = SampleEnvKit;
                row.Changed = true;
            }
            else
            {
                envSrc = Fsx.ReadFileX(filepath, DetectEncoding(filepath));
            }

            if (JsTrim(envSrc).Length == 0)
            {
                envSrc = SampleEnvKit;
                row.Changed = true;
            }

            string? publicKey = PublicKeys(envSrc).FirstOrDefault();

            if (string.IsNullOrEmpty(publicKey))
            {
                if (!noCreate && !_noArmor)
                {
                    _noArmor = !KeyStorage.ChooseArmored();
                }

                // upsert public key to .env file
                (string newPublicKey, string privateKey) = Keypair();
                publicKey = newPublicKey;
                string publicKeyName = GuessPublicKeyName(envFilePath);
                string privateKeyName = GuessPrivateKeyName(envFilePath);
                envSrc = MutateSrc(envSrc, envFilePath, fk, publicKeyName, publicKey);
                if (_noArmor)
                {
                    _keysSrc = MutateKeysSrc(_keysSrc, privateKeyName, privateKey, Path.GetFileName(envFilePath));
                }
                else
                {
                    KeyStorage.StoreInArmor(publicKey, privateKey);

                    // don't set keysSrc (in armor)
                }
            }

            ScanResult scanned = Scan(envSrc, new ScanOptions { Ik = keys, Ek = excludeKeys });
            foreach ((string key, List<string> values) in scanned.Parsed)
            {
                if (IsPublicKey(key, "") || IsPlainKey(key))
                {
                    continue;
                }

                string[] transformed = values.Select(value => Encrypted(value) ? value : Encrypt(value, publicKey))
                    .ToArray();

                string before = envSrc;
                envSrc = Upsert(envSrc, key, transformed);
                if (envSrc != before)
                {
                    row.Changed = true;
                    row.Keys.Add(key);
                }
            }

            row.PublicKey = publicKey;
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
