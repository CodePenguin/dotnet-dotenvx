using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/set.js. Like the original: `publickeys(src)[0]` is the key to
/// encrypt with; a keypair is only generated when encryption is wanted and the file has no public key (the
/// private key is appended to the returned `KeysSrc`, which callers write to the keys file - default
/// `.env.keys` in the working directory). A `*_PLAIN` key, or `--plain`, stores the value as plaintext via
/// `upsert`. When encrypting, the current decrypted value comes from the `get` resolver and the file is only
/// rewritten when the requested value differs from it.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Sets
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

    /// <summary>`noArmor`; see <see cref="Encrypt.NoArmor" />.</summary>
    public bool NoArmor { get; set; } = true;

    private bool _noArmor;
    private readonly List<ProcessedSet> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];
    private readonly string? _value;

    private string? _keysSrc;

    /// <summary>How missing private keys are looked up: --no-native, --no-armor, --token (providers/index.js options).</summary>
    public KeyAccessOptions KeyAccess { get; set; } = new();

    public bool NoNative
    {
        get => KeyAccess.NoNative;
        set => KeyAccess.NoNative = value;
    }

    public Sets(string? key, string? value, EnvItem[] envs, bool encrypt = true, string? envKeysFilePath = null,
        bool noCreate = false)
    {
        _key = key;
        _value = value;
        _encrypt = encrypt;
        _envKeysFilePath = envKeysFilePath;
        _noCreate = noCreate;

        Dictionary<string, string> processEnv = new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        _envs = DetermineEnvs(envs, processEnv);
    }

    protected Sets() : this(null, null, [])
    {
        // For unit tests
    }

    public static Func<string?, string?, EnvItem[], bool, string?, bool, Sets>
        Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual SetsResult Run()
    {
        string fk = string.IsNullOrEmpty(_envKeysFilePath) ? ".env.keys" : _envKeysFilePath;
        _noArmor = NoArmor; // key storage selector below may change it

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

        foreach (EnvItem env in _envs)
        {
            if (env.Type == EnvType.EnvFile)
            {
                SetEnvFile(env, fk);
            }
        }

        return new SetsResult
        {
            KeysSrc = _keysSrc,
            ProcessedEnvs = _processedEnvs.ToArray(),
            ChangedFilePaths = _changedFilePaths.ToArray(),
            UnchangedFilePaths = _unchangedFilePaths.ToArray()
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

            // set.js: `const noEncrypt = !options.encrypt || isPlainKey(key)`
            bool noEncrypt = !_encrypt || IsPlainKey(_key ?? "");

            // only create if missing public key and encryption needed
            if (string.IsNullOrEmpty(publicKey) && !noEncrypt)
            {
                if (!_noCreate && !_noArmor)
                {
                    _noArmor = !KeyStorage.ChooseArmored();
                }

                (string newPublicKey, string privateKey) = Keypair();
                publicKey = newPublicKey;
                string publicKeyName = GuessPublicKeyName(envFilePath);
                string privateKeyName = GuessPrivateKeyName(envFilePath);
                envSrc = MutateSrc(envSrc, envFilePath, fk, publicKeyName, publicKey);
                if (_noArmor)
                {
                    _keysSrc = MutateKeysSrc(_keysSrc, privateKeyName, privateKey, NodePath.Basename(envFilePath));
                }
                else
                {
                    KeyStorage.StoreInArmor(publicKey, privateKey);

                    // don't set keysSrc (in armor)
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
                GetResult resolved = getService.Run();
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
