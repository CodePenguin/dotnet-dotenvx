using System.Collections;
using System.Text;

using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Services;

public class Sets
{
    private readonly HashSet<string> _changedFilepaths = [];
    private readonly bool _encrypt;
    private readonly string? _envKeysFilepath;
    private readonly List<EnvItem> _envs;
    private readonly string? _key;

    private readonly List<ProcessedSet> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilepaths = [];
    private readonly string? _value;

    public Sets(string? key, string? value, List<EnvItem> envs, bool encrypt, string? envKeysFilepath)
    {
        _key = key;
        _value = value;
        _encrypt = encrypt;
        _envKeysFilepath = envKeysFilepath;

        Dictionary<string, string> processEnv = new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        _envs = Helpers.Helpers.DetermineEnvs(envs, processEnv);
    }

    public SetsResult Run()
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
            ProcessedEnvs = _processedEnvs,
            ChangedFilePaths = _changedFilepaths.ToList(),
            UnchangedFilePaths = _unchangedFilepaths.ToList()
        };
    }

    private void SetEnvFile(string envFilepath)
    {
        ProcessedSet row = new() { Key = _key, Value = _value, Type = EnvType.EnvFile };

        string filename = Path.GetFileName(envFilepath);
        string filepath = Path.GetFullPath(envFilepath);
        row.FilePath = filepath;
        row.EnvFilePath = envFilepath;
        row.Changed = false;

        try
        {
            Encoding encoding = DetectEncoding.Execute(filepath);
            string envSrc = File.ReadAllText(filepath, encoding);
            Dictionary<string, object> envParsed = Helpers.Helpers.DotenvParse(envSrc);
            row.OriginalValue = (string?)envParsed.GetValueOrDefault(_key ?? "");
            bool wasPlainText = !Helpers.Helpers.IsEncrypted(row.OriginalValue ?? "");

            if (_encrypt)
            {
                string? publicKey;
                string? privateKey = null;

                string publicKeyName = Helpers.Helpers.GuessPublicKeyName(envFilepath);
                string privateKeyName = Helpers.Helpers.GuessPrivateKeyName(envFilepath);
                string? existingPrivateKey = Helpers.Helpers.FindPrivateKey(envFilepath, _envKeysFilepath);
                string? existingPublicKey = Helpers.Helpers.FindPublicKey(envFilepath, _envKeysFilepath);

                string envKeysFilepath = Path.Combine(Path.GetDirectoryName(filepath)!, ".env.keys");
                if (_envKeysFilepath != null)
                {
                    envKeysFilepath = Path.GetFullPath(_envKeysFilepath);
                }

                string relativeFilepath = Path.GetRelativePath(Path.GetDirectoryName(filepath)!, envKeysFilepath);

                if (existingPrivateKey != null)
                {
                    (string PublicKey, string PrivateKey) kp = Helpers.Helpers.DeriveKeyPair(existingPrivateKey);
                    publicKey = kp.PublicKey;
                    privateKey = kp.PrivateKey;

                    if (row.OriginalValue != null)
                    {
                        row.OriginalValue =
                            Helpers.Helpers.DecryptKeyValue(_key!, row.OriginalValue, privateKeyName, privateKey);
                    }

                    // if derivation doesn't match what's in the file (or preset in env)
                    if (existingPublicKey != null && existingPublicKey != publicKey)
                    {
                        throw new InvalidDotenvPrivateKeyException(privateKey, existingPublicKey);
                    }

                    // typical scenario when encrypting a monorepo second .env file from a prior generated -fk .env.keys file
                    if (existingPublicKey == null)
                    {
                        (string FirstLinePreserved, string EnvSrc) ps = PreserveShebang(envSrc);
                        string firstLinePreserved = ps.FirstLinePreserved;
                        envSrc = ps.EnvSrc;

                        string prependPublicKey =
                            PrependPublicKey(publicKeyName, publicKey, filename, relativeFilepath);

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
                    if (File.Exists(envKeysFilepath))
                    {
                        keysSrc = File.ReadAllText(envKeysFilepath);
                    }

                    (string FirstLinePreserved, string EnvSrc) ps = PreserveShebang(envSrc);
                    string firstLinePreserved = ps.FirstLinePreserved;
                    envSrc = ps.EnvSrc;

                    (string PublicKey, string PrivateKey)
                        kp = Helpers.Helpers.DeriveKeyPair(); // generates a fresh keypair in memory
                    publicKey = kp.PublicKey;
                    privateKey = kp.PrivateKey;

                    string prependPublicKey = PrependPublicKey(publicKeyName, publicKey, filename, relativeFilepath);

                    // privateKey
                    string[] firstTimeKeysSrc =
                    [
                        "#/------------------!DOTENV_PRIVATE_KEYS!-------------------/",
                        "#/ private decryption keys. DO NOT commit to source control /",
                        "#/     [how it works](https://dotenvx.com/encryption)       /",
                        "#/----------------------------------------------------------/"
                    ];
                    string[] appendPrivateKey = [$"# {filename}", $"{privateKeyName}={privateKey}", ""];

                    envSrc = $"{firstLinePreserved}{prependPublicKey}\n{envSrc}";
                    keysSrc = string.IsNullOrEmpty(keysSrc)
                        ? string.Join("\n", firstTimeKeysSrc) + "\n"
                        : keysSrc + "\n";
                    keysSrc += string.Join("\n", appendPrivateKey);

                    // write to .env.keys
                    File.WriteAllText(envKeysFilepath, keysSrc);

                    row.PrivateKeyAdded = true;
                    row.EnvKeysFilePath = _envKeysFilepath ?? Path.Combine(Path.GetDirectoryName(envFilepath)!,
                        Path.GetFileName(envKeysFilepath));
                }

                row.PublicKey = publicKey;
                row.PrivateKey = privateKey;
                row.EncryptedValue = Helpers.Helpers.EncryptValue(_value ?? "", publicKey);
                row.PrivateKeyName = privateKeyName;
            }

            bool goingFromPlainTextToEncrypted = wasPlainText && _encrypt;
            bool valueChanged = (_value ?? "") != (row.OriginalValue ?? "");
            if (goingFromPlainTextToEncrypted || valueChanged)
            {
                row.EnvSrc = Helpers.Helpers.Replace(envSrc, _key ?? "", row.EncryptedValue ?? _value ?? "");
                _changedFilepaths.Add(envFilepath);
                row.Changed = true;
            }
            else
            {
                row.EnvSrc = envSrc;
                _unchangedFilepaths.Add(envFilepath);
                row.Changed = false;
            }
        }
        catch (Exception e)
        {
            row.Error = e is FileNotFoundException ? new MissingEnvFileException(envFilepath, filepath) : e;
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
        string relativeFilepath)
    {
        string comment = relativeFilepath == ".env.keys" ? "" : $" # -fk {relativeFilepath}";

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