using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/encrypt.js. The public key is `publickeys(src)[0]` (the
/// first DOTENV_PUBLIC_KEY* in the file, whatever its name); when there is none a fresh keypair is made, the
/// public key header is prepended to the env source and the private key is handed to the chosen custodian (the keys
/// file text comes back as `KeysSrc`; callers write it). Values are chosen with `scan` (`ik`/`ek`) and the Envfile
/// (`fs.encrypted`, else "not *_PLAIN"), encrypted unless already encrypted, and upserted per occurrence; DOTENV_PUBLIC_KEY*
/// keys are never encrypted. When an Envfile exempts everything, no key is created.
/// </remarks>
internal class Encrypt(EnvItem[] envs, string[]? keys, string[]? excludeKeys, string? envKeysFilePath,
    bool noCreate = false)
{
    private const string SampleEnvKit = "HELLO=\"World\"\n";

    private static readonly System.Text.RegularExpressions.Regex WithPublicKeyFormat =
        new(@"^(?:0[23][0-9a-f]{64}|04[0-9a-f]{128})\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private readonly HashSet<string> _changedFilePaths = [];
    private readonly List<EncryptProcessedEnv> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];

    private string? _keysSrc;

    /// <summary>How private keys are looked up and stored: --no-native, --no-armor, --no-1password, --no-bitwarden, --token.</summary>
    public KeyAccessOptions KeyAccess { get; set; } = new() { NoArmor = true };

    /// <summary>`--profile`: the Envfile profile that selects the env files.</summary>
    public string? Profile { get; set; }

    /// <summary>`--with <public-key>`: encrypt with a public key created elsewhere (no private key is made or stored).</summary>
    public string? With { get; set; }

    public EncryptResult Run()
    {
        string fk = string.IsNullOrEmpty(envKeysFilePath) ? ".env.keys" : envKeysFilePath;
        string? storage = null;
        StoreContext custodyContext = new() { Token = KeyAccess.Token };
        KeyStorageOptions storageOptions = new() { Access = KeyAccess, NoCreate = noCreate, Fk = fk };

        // set up keysSrc
        string fkPath = NodePath.Resolve(fk);
        if (With is null && Fsx.ExistsSync(fkPath))
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

        foreach (EnvItem env in SelectEnvfileEnvs(envs, new SelectEnvfileOptions { Profile = Profile }))
        {
            if (env.Type == EnvType.EnvFile)
            {
                EncryptEnvFile(env.Value, fk, ref storage, custodyContext, storageOptions);
            }
        }

        return new EncryptResult
        {
            KeysSrc = _keysSrc,
            ProcessedEnvs = [.. _processedEnvs],
            ChangedFilePaths = [.. _changedFilePaths],
            UnchangedFilePaths = [.. _unchangedFilePaths]
        };
    }

    private void EncryptEnvFile(string envFilePath, string fk, ref string? storage, StoreContext custodyContext,
        KeyStorageOptions storageOptions)
    {
        EncryptProcessedEnv row = new() { Keys = [], Type = EnvType.EnvFile };

        string filepath = NodePath.Resolve(envFilePath);
        row.FilePath = filepath;
        row.EnvFilePath = envFilePath;

        try
        {
            EnvfileSchema schema = ReadEnvfile.Read(null, [filepath]);
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

            if (!schema.Exists && JsTrim(envSrc).Length == 0)
            {
                envSrc = SampleEnvKit;
                row.Changed = true;
            }

            if (With is not null)
            {
                // Inspect every assignment before selection or schema rules can hide it.
                ScanResult existing = Scan(envSrc, new ScanOptions());
                List<string> declaredKeys = [.. existing.Parsed.Where(pair => IsPublicKey(pair.Key, "")).SelectMany(pair => pair.Value)];
                bool matchingKey = declaredKeys.Count > 0 && declaredKeys.All(value =>
                    string.Equals(value, With, StringComparison.OrdinalIgnoreCase));
                if (!matchingKey && (declaredKeys.Count > 0 || existing.Parsed.Values.Any(values => values.Any(Encrypted))))
                {
                    throw new AlreadyEncryptedException(envFilePath);
                }

                string withPublicKeyName = GuessPublicKeyName(envFilePath);
                try
                {
                    if (!WithPublicKeyFormat.IsMatch(With))
                    {
                        throw new FormatException("Invalid public key format");
                    }

                    Primitives.Primitives.Encrypt("", With); // Validate the curve point even if no values need encryption.
                }
                catch (Exception)
                {
                    throw new InvalidPublicKeyException(withPublicKeyName, With);
                }

                if (!matchingKey)
                {
                    envSrc = MutateSrc(envSrc, envFilePath, "", withPublicKeyName, With);
                    row.Changed = true;
                }
            }

            ScanResult scanned = Scan(envSrc, new ScanOptions { Ik = keys, Ek = excludeKeys });
            List<KeyValuePair<string, List<string>>> selected = [.. scanned.Parsed.Where(pair =>
            {
                if (IsPublicKey(pair.Key, ""))
                {
                    return false;
                }

                return schema.Exists
                    ? !schema.Keys.TryGetValue(pair.Key, out KeyPolicy? policy) || policy.Encrypted
                    : !IsPlainKey(pair.Key);
            })];
            if (schema.Exists && !selected.Any(pair => pair.Value.Any(value => !Encrypted(value))))
            {
                // A missing file still needs writing even when its initial values are exempt from encryption.
                // Avoid generating keys, but preserve creation.
                row.EnvSrc = envSrc;
                if (row.Changed)
                {
                    _changedFilePaths.Add(envFilePath);
                }
                else
                {
                    _unchangedFilePaths.Add(envFilePath);
                }

                _processedEnvs.Add(row);

                return;
            }

            string? publicKey = PublicKeys(envSrc).FirstOrDefault();

            if (string.IsNullOrEmpty(publicKey))
            {
                // upsert public key to .env file
                (string newPublicKey, string privateKey) = Keypair();
                publicKey = newPublicKey;
                string publicKeyName = GuessPublicKeyName(envFilePath);
                string privateKeyName = GuessPrivateKeyName(envFilePath);
                envSrc = MutateSrc(envSrc, envFilePath, fk, publicKeyName, publicKey);

                custodyContext.KeysSrc = _keysSrc;
                custodyContext.PrivateKeyName = privateKeyName;
                custodyContext.Comment = NodePath.Basename(envFilePath);
                custodyContext.KeysFilepath = fk;
                (string chosen, StoreResult stored) =
                    KeyStorage.Store(storage, publicKey, privateKey, storageOptions, custodyContext);
                storage = chosen;
                row.KeySources = new Dictionary<string, string> { [publicKey] = stored.HasKeysSrc ? "file" : storage };
                if (stored.HasKeysSrc)
                {
                    _keysSrc = stored.KeysSrc;
                }

                if (stored.NativePrivateKeyAdded)
                {
                    row.NativePrivateKeyAdded = true;
                }
            }

            foreach ((string key, List<string> values) in selected)
            {
                string[] transformed = [.. values.Select(value => Encrypted(value) ? value : Encrypt(value, publicKey))];

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
