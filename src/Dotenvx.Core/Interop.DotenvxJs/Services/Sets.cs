using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/set.js. Like the original: `publickeys(src)[0]` is the key to
/// encrypt with; a keypair is only generated when encryption is wanted and the file has no public key (the private key goes
/// to the chosen custodian; the keys file text comes back as `KeysSrc`, which callers write - default `.env.keys` in the
/// working directory). A key the Envfile exempts from encryption (else a `*_PLAIN` key), or `--plain`, stores the value as
/// plaintext via `upsert`. When encrypting, the current decrypted value comes from the `get` resolver and the file is only
/// rewritten when the requested value differs from it.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
internal class Sets
{
    private static readonly Func<string?, string?, EnvItem[], bool, string?, bool, Sets> DefaultFactory =
        (key, value, envs, encrypt, envKeysFile, noCreate) =>
            new Sets(key, value, envs, encrypt, envKeysFile, noCreate);

    private readonly HashSet<string> _changedFilePaths = [];
    private readonly bool _encrypt;
    private readonly string? _envKeysFilePath;
    private readonly EnvItem[] _envs;
    private readonly string? _key;

    private readonly bool _noCreate;

    private readonly List<ProcessedSet> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];
    private readonly string? _value;

    private string? _keysSrc;
    private StorageChoice? _storage;
    private readonly StoreContext _custodyContext = new();

    /// <summary>How private keys are looked up and stored: --no-native, --no-armor, --no-1password, --no-bitwarden, --token.</summary>
    public KeyAccessOptions KeyAccess { get; set; } = new() { NoArmor = true };

    public bool NoNative
    {
        get => KeyAccess.NoNative;
        set => KeyAccess.NoNative = value;
    }

    /// <summary>`--profile`: the Envfile profile that selects the env files.</summary>
    public string? Profile { get; set; }

    public Sets(string? key, string? value, EnvItem[] envs, bool encrypt = true, string? envKeysFilePath = null,
        bool noCreate = false)
    {
        _key = key;
        _value = value;
        _encrypt = encrypt;
        _envKeysFilePath = envKeysFilePath;
        _noCreate = noCreate;
        _envs = envs;
    }

    protected Sets() : this(null, null, [])
    {
        // For unit tests
    }

    public static Func<string?, string?, EnvItem[], bool, string?, bool, Sets>
        Factory
    { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual SetsResult Run()
    {
        string fk = string.IsNullOrEmpty(_envKeysFilePath) ? ".env.keys" : _envKeysFilePath;
        _custodyContext.Token = KeyAccess.Token;

        // set up keysSrc
        string fkPath = NodePath.Resolve(fk);
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

        foreach (EnvItem env in SelectEnvfileEnvs(_envs, new SelectEnvfileOptions { Profile = Profile }))
        {
            if (env.Type == EnvType.EnvFile)
            {
                SetEnvFile(env, fk);
            }
        }

        return new SetsResult
        {
            KeysSrc = _keysSrc,
            ProcessedEnvs = [.. _processedEnvs],
            ChangedFilePaths = [.. _changedFilePaths],
            UnchangedFilePaths = [.. _unchangedFilePaths]
        };
    }

    private void SetEnvFile(EnvItem env, string fk)
    {
        string envFilePath = env.Value;
        ProcessedSet row = new() { Key = _key, Value = _value, Type = EnvType.EnvFile };

        string filePath = NodePath.Resolve(envFilePath);
        row.FilePath = filePath;
        row.EnvFilePath = envFilePath;
        row.Changed = false;

        try
        {
            EnvfileSchema schema = ReadEnvfile.Read(null, [filePath]);

            // set.js: noEncrypt = !options.encrypt || (schema.exists ? schema.keys[key]?.fs.encrypted === false : isPlainKey(key))
            bool noEncrypt = !_encrypt || (schema.Exists
                ? schema.Keys.TryGetValue(_key ?? "", out KeyPolicy? policy) && !policy.Encrypted
                : IsPlainKey(_key ?? ""));

            string envSrc;
            if (!Fsx.ExistsSync(filePath) && !_noCreate)
            {
                envSrc = "";
                row.Changed = true;
            }
            else
            {
                envSrc = Fsx.ReadFileX(filePath, DetectEncoding(filePath));
            }

            if (JsTrim(envSrc).Length == 0)
            {
                envSrc = "";
                row.Changed = true;
            }

            string? publicKey = PublicKeys(envSrc).FirstOrDefault();

            // only create if missing public key and encryption needed
            if (string.IsNullOrEmpty(publicKey) && !noEncrypt)
            {
                // upsert public key to .env file
                string publicKeyName = GuessPublicKeyName(envFilePath);
                string privateKeyName = GuessPrivateKeyName(envFilePath);

                _custodyContext.EnvFilepath = filePath;
                _custodyContext.KeysSrc = _keysSrc;
                _custodyContext.PrivateKeyName = privateKeyName;
                _custodyContext.Comment = NodePath.Basename(envFilePath);
                _custodyContext.KeysFilepath = fk;
                (StorageChoice chosen, StoreResult stored, string storedPublicKey) = KeyStorage.Store(_storage, null, null,
                    new KeyStorageOptions { Access = KeyAccess, NoCreate = _noCreate, Fk = fk }, _custodyContext);
                _storage = chosen;
                publicKey = storedPublicKey;
                envSrc = MutateSrc(envSrc, envFilePath, fk, publicKeyName, publicKey);
                row.KeySources = new Dictionary<string, string> { [publicKey] = stored.HasKeysSrc ? "file" : chosen.Id };
                if (stored.HasKeysSrc)
                {
                    _keysSrc = stored.KeysSrc;
                }

                if (stored.NativePrivateKeyAdded)
                {
                    row.NativePrivateKeyAdded = true;
                }
            }

            if (noEncrypt)
            {
                string before = envSrc;
                envSrc = Upsert(envSrc, _key ?? "", _value ?? "");
                if (envSrc != before)
                {
                    row.Changed = true;
                }
            }
            else
            {
                // expensive additional loop: the current (decrypted) value comes from the get resolver
                Get getService = Get.Factory(_key, [env], false, true, [fk]);
                getService.KeyAccess = KeyAccess;
                getService.No1Password = KeyAccess.No1Password;
                getService.NoBitwarden = KeyAccess.NoBitwarden;
                getService.IncludeKeySources = true;
                getService.PromptForSecrets = true;
                GetResult resolved = getService.Run();
                row.KeySources ??= resolved.KeySources;
                string? before = resolved.Parsed.GetValueOrDefault(_key ?? "");
                if (_value != before)
                {
                    envSrc = Upsert(envSrc, _key ?? "", Encrypt(_value ?? "", publicKey!));
                    row.Changed = true;
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
