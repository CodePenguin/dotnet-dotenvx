using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/encrypt.js</remarks>
public class Encrypt(EnvItem[] envs, string[]? keys, string[]? excludeKeys, string? envKeysFilePath)
{
    private readonly HashSet<string> _changedFilePaths = [];

    private readonly EnvItem[] _envs = DetermineEnvs(envs, Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

    private readonly List<EncryptProcessedEnv> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];

    private Func<string, bool> _exclude = _ => false;
    private Func<string, bool> _include = _ => true;

    public EncryptResult Run()
    {
        _exclude = Picomatch(excludeKeys);
        _include = Picomatch(keys);

        foreach (EnvItem env in _envs)
        {
            if (env.Type == EnvType.EnvFile)
            {
                EncryptEnvFile(env.Value);
            }
        }

        return new EncryptResult
        {
            ProcessedEnvs = _processedEnvs.ToArray(),
            ChangedFilePaths = _changedFilePaths.ToArray(),
            UnchangedFilePaths = _unchangedFilePaths.ToArray()
        };
    }

    private void EncryptEnvFile(string envFilePath)
    {
        EncryptProcessedEnv row = new() { Keys = [], Type = EnvType.EnvFile };

        string filename = Path.GetFileName(envFilePath);
        string filepath = Path.GetFullPath(envFilePath);
        row.FilePath = filepath;
        row.EnvFilePath = envFilePath;

        try
        {
            string encoding = DetectEncoding(filepath);
            string envSrc = Fsx.ReadFileX(filepath, encoding);
            Dictionary<string, object> envParsed = DotenvParse(envSrc);

            string? publicKey;
            string? privateKey = null;

            string publicKeyName = GuessPublicKeyName(envFilePath);
            string privateKeyName = GuessPrivateKeyName(envFilePath);
            string? existingPrivateKey = FindPrivateKey(envFilePath, envKeysFilePath);
            string? existingPublicKey = FindPublicKey(envFilePath);

            string localEnvKeysFilePath = Path.Combine(Path.GetDirectoryName(filepath)!, ".env.keys");
            if (!string.IsNullOrEmpty(envKeysFilePath))
            {
                localEnvKeysFilePath = Path.GetFullPath(envKeysFilePath);
            }

            string relativeFilePath = Path.GetRelativePath(Path.GetDirectoryName(filepath)!, localEnvKeysFilePath);

            if (!string.IsNullOrEmpty(existingPrivateKey))
            {
                (string PublicKey, string PrivateKey) kp = KeyPair(existingPrivateKey);
                publicKey = kp.PublicKey;
                privateKey = kp.PrivateKey;

                if (!string.IsNullOrEmpty(existingPublicKey) && existingPublicKey != publicKey)
                {
                    throw new InvalidDotenvPrivateKeyException(
                        publicKeyName,
                        publicKey,
                        existingPublicKey,
                        privateKeyName,
                        existingPrivateKey);
                }

                if (string.IsNullOrEmpty(existingPublicKey))
                {
                    (string firstLinePreserved, string psEnvSrc) = PreserveShebang(envSrc);
                    envSrc = psEnvSrc;

                    string prependPublicKey = PrependPublicKey(publicKeyName, publicKey, filename, relativeFilePath);

                    envSrc = $"{firstLinePreserved}{prependPublicKey}\n{envSrc}";
                }
            }
            else if (!string.IsNullOrEmpty(existingPublicKey))
            {
                publicKey = existingPublicKey;
            }
            else
            {
                string keysSrc = "";

                if (Fsx.ExistsSync(localEnvKeysFilePath))
                {
                    keysSrc = Fsx.ReadFileX(localEnvKeysFilePath);
                }

                (string FirstLinePreserved, string EnvSrc) ps = PreserveShebang(envSrc);
                string firstLinePreserved = ps.FirstLinePreserved;
                envSrc = ps.EnvSrc;

                (string PublicKey, string PrivateKey) kp = KeyPair();
                publicKey = kp.PublicKey;
                privateKey = kp.PrivateKey;

                string prependPublicKey = PrependPublicKey(publicKeyName, publicKey, filename, relativeFilePath);

                string firstTimeKeysSrc = string.Join("\n",
                    "#/------------------!DOTENV_PRIVATE_KEYS!-------------------/",
                    "#/ private decryption keys. DO NOT commit to source control /",
                    "#/     [how it works](https://dotenvx.com/encryption)       /",
                    "#/----------------------------------------------------------/");
                string appendPrivateKey = string.Join("\n", $"# {filename}", $"{privateKeyName}={privateKey}", "");

                envSrc = $"{firstLinePreserved}{prependPublicKey}\n{envSrc}";
                keysSrc = keysSrc.Length > 1 ? keysSrc : $"{firstTimeKeysSrc}\n";
                keysSrc = $"{keysSrc}\n{appendPrivateKey}";

                Fsx.WriteFileX(localEnvKeysFilePath, keysSrc);

                row.PrivateKeyAdded = true;
                row.EnvKeysFilePath = envKeysFilePath ?? Path.Combine(Path.GetDirectoryName(envFilePath)!,
                    Path.GetFileName(localEnvKeysFilePath));
            }

            row.PublicKey = publicKey;
            row.PrivateKey = privateKey;
            row.PrivateKeyName = privateKeyName;

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

                bool encrypted = IsEncrypted(value) || IsPublicKey(envKey, value);
                if (encrypted)
                {
                    continue;
                }

                row.Keys.Add(envKey);

                string encryptedValue = EncryptValue(value, publicKey);

                envSrc = Replace(envSrc, envKey, encryptedValue);

                row.Changed = true;
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
            row.Error = e is FileNotFoundException ? new MissingEnvFileException(envFilePath, filepath) : e;
        }

        _processedEnvs.Add(row);
    }

    private static string PrependPublicKey(string publicKeyName, string publicKey, string filename,
        string relativeFilePath = "")
    {
        string comment = relativeFilePath == ".env.keys" ? "" : $" # {relativeFilePath}";

        return string.Join("\n",
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"{publicKeyName}=\"{publicKey}\"{comment}", "", $"# {filename}");
    }

    private static (string FirstLinePreserved, string EnvSrc) PreserveShebang(string envSrc)
    {
        string[] lines = envSrc.Split('\n');
        string firstLine = lines[0];
        string[] remainingLines = lines.Skip(1).ToArray();
        string firstLinePreserved = "";

        if (!firstLine.StartsWith("#!"))
        {
            return (firstLinePreserved, envSrc);
        }

        firstLinePreserved = firstLine + "\n";
        envSrc = string.Join('\n', remainingLines);

        return (firstLinePreserved, envSrc);
    }
}