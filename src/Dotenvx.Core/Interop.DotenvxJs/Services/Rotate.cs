using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.PicomatchJs.IndexJs;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/rotate.js - this file (and the `rotate` CLI command
/// itself) does NOT exist anywhere in the submodule at this v2.24.1 pin at all (confirmed
/// 2026-09-24, no `rotate` registration in dotenvx.js) - `rotate` is a feature this port added
/// ahead of the pin, not a line-for-line port of pinned upstream source, unlike every other
/// service in this directory.
///
/// Correction, 2026-09-26: a real historical version of this exact file DOES exist in the
/// submodule's own git history, at commit f1ffd157 (2026-06-29) - the last revision before it was
/// deleted in 8af55792 ("remove rotate for 2.0. to return in more complete fashion for 2.1"). Used
/// that to fix several real bugs in RotateAction.cs (see its own class remarks) - this SERVICE
/// class's own algorithm has NOT yet been re-audited line-by-line against that historical source
/// (only the CLI action's message-printing/control-flow layer was, since that's where today's
/// bug-hunting sweep was focused); worth a dedicated pass later if `rotate` needs closer scrutiny.
/// </remarks>
public class Rotate(
    EnvItem[]? envs = null,
    string[]? key = null,
    string[]? excludeKey = null,
    string? envKeysFilePath = null)
{
    private static readonly Func<EnvItem[]?, string[]?, string[]?, string?, Rotate> DefaultFactory =
        (envs, key, excludeKey, envKeysFilePath) => new Rotate(envs, key, excludeKey, envKeysFilePath);

    private readonly HashSet<string> _changedFilePaths = [];

    private readonly Dictionary<string, string> _envKeysSources = new();

    private readonly EnvItem[] _envs = DetermineEnvs(envs, Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));

    private readonly List<RotateProcessedEnv> _processedEnvs = [];
    private readonly HashSet<string> _unchangedFilePaths = [];

    private Func<string, bool> _exclude = _ => false;
    private Func<string, bool> _include = _ => true;

    protected Rotate() : this(null)
    {
        // For unit tests
    }

    public static Func<EnvItem[]?, string[]?, string[]?, string?, Rotate> Factory { get; internal set; } =
        DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual RotateResult Run()
    {
        _exclude = Picomatch(excludeKey);
        _include = Picomatch(key, excludeKey ?? []);

        foreach (EnvItem env in _envs)
        {
            if (env.Type == EnvType.EnvFile)
            {
                RotateEnvFile(env.Value);
            }
        }

        return new RotateResult
        {
            ProcessedEnvs = _processedEnvs.ToArray(),
            ChangedFilePaths = _changedFilePaths.ToArray(),
            UnchangedFilePaths = _unchangedFilePaths.ToArray()
        };
    }

    private void RotateEnvFile(string envFilepath)
    {
        RotateProcessedEnv row = new()
        {
            Keys = [],
            Type = EnvType.EnvFile,
            FilePath = Path.GetFullPath(envFilepath),
            EnvFilePath = envFilepath,
            Changed = false,
            PrivateKeyAdded = false,
            EnvSrc = "",
            EnvKeysSrc = "",
            EnvKeysFilePath = ""
        };

        try
        {
            string encoding = DetectEncoding(row.FilePath);
            string envSrc = Fsx.ReadFileX(row.FilePath, encoding);
            Dictionary<string, object> envParsedObj = DotenvParse(envSrc);
            Dictionary<string, string> envParsed = envParsedObj.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value);

            string publicKeyName = GuessPublicKeyName(envFilepath);
            string privateKeyName = GuessPrivateKeyName(envFilepath);
            string? existingPrivateKey = FindPrivateKey(envFilepath, envKeysFilePath);

            string localEnvKeysFilePath = Path.Combine(Path.GetDirectoryName(row.FilePath)!, ".env.keys");
            if (!string.IsNullOrEmpty(envKeysFilePath))
            {
                localEnvKeysFilePath = Path.GetFullPath(envKeysFilePath);
            }

            row.EnvKeysFilePath = localEnvKeysFilePath;
            if (!_envKeysSources.TryGetValue(localEnvKeysFilePath, out string? value))
            {
                string keysEncoding = DetectEncoding(localEnvKeysFilePath);
                value = Fsx.ReadFileX(localEnvKeysFilePath, keysEncoding);
                _envKeysSources[localEnvKeysFilePath] = value;
            }

            string envKeysSrc = value;

            // New keypair
            (string PublicKey, string PrivateKey) nkp = KeyPair();
            string newPublicKey = nkp.PublicKey;
            string newPrivateKey = nkp.PrivateKey;

            // .env
            envSrc = Replace(envSrc, publicKeyName, newPublicKey);
            row.Changed = true;
            foreach (KeyValuePair<string, string> kvp in envParsed)
            {
                string k = kvp.Key;
                string v = kvp.Value;

                if (_exclude(k))
                {
                    continue;
                }

                if (!_include(k))
                {
                    continue;
                }

                if (!IsEncrypted(v))
                {
                    continue;
                }

                row.Keys = [.. row.Keys, k];

                string decryptedValue = DecryptKeyValue(k, v, privateKeyName, existingPrivateKey);
                string encryptedValue = EncryptValue(decryptedValue, newPublicKey);

                envSrc = Replace(envSrc, k, encryptedValue);
            }

            row.EnvSrc = envSrc;

            // .env.keys
            row.PrivateKeyAdded = true;
            row.PrivateKeyName = privateKeyName;
            row.PrivateKey = newPrivateKey;
            envKeysSrc = Append(envKeysSrc, privateKeyName, newPrivateKey);
            _envKeysSources[localEnvKeysFilePath] = envKeysSrc;
            row.EnvKeysSrc = envKeysSrc;
        }
        catch (FileNotFoundException)
        {
            row.Error = new MissingEnvFileException(envFilepath);
        }
        catch (Exception e)
        {
            row.Error = e;
        }

        _processedEnvs.Add(row);

        if (row.Changed)
        {
            _changedFilePaths.Add(envFilepath);
        }
        else
        {
            _unchangedFilePaths.Add(envFilepath);
        }
    }
}