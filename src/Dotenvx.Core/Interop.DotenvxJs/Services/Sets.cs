using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/transforms/set.js (moved there from
/// src/lib/services/sets.js, which no longer exists at this pin - confirmed 2026-09-24, part of
/// the same custodian/keyring reorg CLAUDE.md already documents elsewhere). Already the file
/// actually read/verified against during this session's error-message-system fix work (including
/// the MispairedPrivateKeyException argument-order bug fix) - this comment was just stale, not the
/// logic.
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
    private readonly List<ProcessedSet> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];
    private readonly string? _value;

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
        foreach (EnvItem env in _envs)
        {
            if (env.Type == EnvType.EnvFile)
            {
                SetEnvFile(env.Value);
            }
        }

        return new SetsResult
        {
            ProcessedEnvs = _processedEnvs.ToArray(),
            ChangedFilePaths = _changedFilePaths.ToArray(),
            UnchangedFilePaths = _unchangedFilePaths.ToArray()
        };
    }

    private void SetEnvFile(string envFilePath)
    {
        ProcessedSet row = new() { Key = _key, Value = _value, Type = EnvType.EnvFile };

        string fileName = Path.GetFileName(envFilePath);
        string filePath = Path.GetFullPath(envFilePath);
        row.FilePath = filePath;
        row.EnvFilePath = envFilePath;
        row.Changed = false;

        try
        {
            string envSrc;
            if (!Fsx.ExistsSync(filePath) && !_noCreate)
            {
                envSrc = "";
            }
            else
            {
                string encoding = DetectEncoding(filePath);
                envSrc = Fsx.ReadFileX(filePath, encoding);
            }

            if (envSrc.Trim().Length == 0)
            {
                envSrc = "";
            }

            Dictionary<string, object> envParsed = ScanToDictionary(envSrc);
            row.OriginalValue = (string?)envParsed.GetValueOrDefault(_key ?? "");

            // set.js: `const noEncrypt = !options.encrypt || isPlainKey(key)` - a key ending in
            // _PLAIN always stores as plaintext regardless of --encrypt/-c, independent of
            // encrypt.js's identical exemption (see Encrypt.cs).
            bool noEncrypt = !_encrypt || IsPlainKey(_key ?? "");

            if (!noEncrypt)
            {
                string? publicKey;
                string? privateKey = null;

                string publicKeyName = GuessPublicKeyName(envFilePath);
                string privateKeyName = GuessPrivateKeyName(envFilePath);
                string? existingPrivateKey = FindPrivateKey(envFilePath, _envKeysFilePath);
                string? existingPublicKey = FindPublicKey(envFilePath, _envKeysFilePath);

                string envKeysFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, ".env.keys");
                if (_envKeysFilePath != null)
                {
                    envKeysFilePath = Path.GetFullPath(_envKeysFilePath);
                }

                string relativeFilePath = Path.GetRelativePath(Path.GetDirectoryName(filePath)!, envKeysFilePath);

                if (existingPrivateKey != null)
                {
                    (string PublicKey, string PrivateKey) kp = Keypair(existingPrivateKey);
                    publicKey = kp.PublicKey;
                    privateKey = kp.PrivateKey;

                    if (row.OriginalValue != null)
                    {
                        row.OriginalValue =
                            DecryptKeyValue(_key!, row.OriginalValue, privateKeyName, privateKey);
                    }

                    // if derivation doesn't match what's in the file (or preset in env)
                    if (existingPublicKey != null && existingPublicKey != publicKey)
                    {
                        throw new MispairedPrivateKeyException(publicKey, existingPublicKey);
                    }

                    // typical scenario when encrypting a monorepo second .env file from a prior generated -fk .env.keys file
                    if (existingPublicKey == null)
                    {
                        (string firstLinePreserved, string s) = PreserveShebang(envSrc);
                        envSrc = s;

                        string prependPublicKey =
                            PrependPublicKey(publicKeyName, publicKey, fileName, relativeFilePath);

                        envSrc = $"{firstLinePreserved}{prependPublicKey}\n{envSrc}";
                    }
                }
                else if (existingPublicKey != null)
                {
                    publicKey = existingPublicKey;
                }
                else
                {
                    // .env.keys
                    string keysSrc = "";
                    if (Fsx.ExistsSync(envKeysFilePath))
                    {
                        keysSrc = Fsx.ReadFileX(envKeysFilePath);
                    }

                    (string FirstLinePreserved, string EnvSrc) ps = PreserveShebang(envSrc);
                    string firstLinePreserved = ps.FirstLinePreserved;
                    envSrc = ps.EnvSrc;

                    (string PublicKey, string PrivateKey)
                        kp = Keypair(); // generates a fresh keypair in memory
                    publicKey = kp.PublicKey;
                    privateKey = kp.PrivateKey;

                    string prependPublicKey = PrependPublicKey(publicKeyName, publicKey, fileName, relativeFilePath);

                    // privateKey
                    string[] firstTimeKeysSrc =
                    [
                        "#/------------------!DOTENV_PRIVATE_KEYS!-------------------/",
                        "#/ private decryption keys. DO NOT commit to source control /",
                        "#/     [how it works](https://dotenvx.com/encryption)       /",
                        "#/----------------------------------------------------------/"
                    ];
                    string[] appendPrivateKey = [$"# {fileName}", $"{privateKeyName}={privateKey}", ""];

                    envSrc = $"{firstLinePreserved}{prependPublicKey}\n{envSrc}";
                    keysSrc = string.IsNullOrEmpty(keysSrc)
                        ? string.Join("\n", firstTimeKeysSrc) + "\n"
                        : keysSrc + "\n";
                    keysSrc += string.Join("\n", appendPrivateKey);

                    // write to .env.keys
                    Fsx.WriteFileX(envKeysFilePath, keysSrc);

                    row.PrivateKeyAdded = true;
                    row.EnvKeysFilePath = _envKeysFilePath ?? Path.Combine(Path.GetDirectoryName(envFilePath)!,
                        Path.GetFileName(envKeysFilePath));
                }

                row.PublicKey = publicKey;
                row.PrivateKey = privateKey;
                row.EncryptedValue = Encrypt(_value ?? "", publicKey);
                row.PrivateKeyName = privateKeyName;
            }

            // set.js compares the requested value against the CURRENT (decrypted, if applicable)
            // value alone - it never force-re-encrypts an unchanged value just because it happened
            // to be stored as plaintext before. Confirmed live: `set FOO bar` on an existing
            // `FOO=bar` (plaintext, encryption on) is "○ no change", not a forced encryption.
            bool valueChanged = (_value ?? "") != (row.OriginalValue ?? "");
            if (valueChanged)
            {
                row.EnvSrc = Upsert(envSrc, _key ?? "", row.EncryptedValue ?? _value ?? "");
                _changedFilePaths.Add(envFilePath);
                row.Changed = true;
            }
            else
            {
                row.EnvSrc = envSrc;
                _unchangedFilePaths.Add(envFilePath);
                row.Changed = false;
            }
        }
        catch (Exception e)
        {
            row.Error = e is FileNotFoundException ? new MissingEnvFileException(envFilePath) : e;
        }

        _processedEnvs.Add(row);
    }

    private static (string FirstLinePreserved, string EnvSrc) PreserveShebang(string envSrc)
    {
        string[] lines = envSrc.Split('\n');
        string firstLine = lines[0];
        string firstLinePreserved = "";

        if (!firstLine.StartsWith("#!"))
        {
            return (firstLinePreserved, envSrc);
        }

        firstLinePreserved = firstLine + "\n";
        envSrc = string.Join("\n", lines.Skip(1));

        return (firstLinePreserved, envSrc);
    }

    private static string PrependPublicKey(string publicKeyName, string publicKey, string filename,
        string relativeFilePath)
    {
        string comment = relativeFilePath == ".env.keys" ? "" : $" # -fk {relativeFilePath}";

        string[] lines =
        [
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"{publicKeyName}=\"{publicKey}\"{comment}", "", $"# {filename}"
        ];

        return string.Join("\n", lines);
    }
}