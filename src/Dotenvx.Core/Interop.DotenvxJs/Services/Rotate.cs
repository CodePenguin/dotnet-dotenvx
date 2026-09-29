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

        string localEnvKeysFilePath = Path.Combine(Path.GetDirectoryName(row.FilePath)!, ".env.keys");
        if (!string.IsNullOrEmpty(envKeysFilePath))
        {
            localEnvKeysFilePath = Path.GetFullPath(envKeysFilePath);
        }

        try
        {
            // The historical service distinguishes a missing .env from a missing .env.keys by
            // comparing the ENOENT error's own path against the expected .env.keys path - .NET has
            // no equivalent structured way to recover that from Fsx.ReadFileX's exception, so this
            // checks existence explicitly instead (confirmed live 2026-09-26: without this, a
            // missing .env.keys wrongly reported "missing file (.env)" even though .env existed).
            // Uses internal marker exceptions rather than throwing the real DotenvxException
            // directly, so row.Error always holds a freshly-constructed (never-thrown) instance -
            // existing tests compare it structurally via ShouldBeEquivalentTo, which is sensitive to
            // a thrown exception's populated TargetSite/StackTrace.
            if (!Fsx.ExistsSync(row.FilePath))
            {
                throw new MissingEnvFileSignal();
            }

            string encoding = DetectEncoding(row.FilePath);
            string envSrc = Fsx.ReadFileX(row.FilePath, encoding);
            Dictionary<string, object> envParsedObj = DotenvParse(envSrc);
            Dictionary<string, string> envParsed = envParsedObj.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value);

            string publicKeyName = GuessPublicKeyName(envFilepath);
            string privateKeyName = GuessPrivateKeyName(envFilepath);
            string? existingPrivateKey = FindPrivateKey(envFilepath, envKeysFilePath);

            row.EnvKeysFilePath = localEnvKeysFilePath;
            if (!_envKeysSources.TryGetValue(localEnvKeysFilePath, out string? value))
            {
                if (!Fsx.ExistsSync(localEnvKeysFilePath))
                {
                    throw new MissingEnvKeysFileSignal();
                }

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

                // Matches the historical service's `if (this.keys.length > 0 && !this.include(key))
                // continue` exactly - the include-filter only applies when --key was actually given.
                // Without this guard, Picomatch(null, someNonEmptyExcludeList) does NOT hit its own
                // "no globs and no ignore list -> match everything" special case (the ignore list
                // isn't empty), so it falls through to "empty include list -> matches nothing",
                // silently skipping re-encryption of EVERY key whenever --exclude-key is given
                // without --key - confirmed live: rotate --exclude-key FOO changed DOTENV_PUBLIC_KEY
                // but left every value (including HELLO, which was never excluded) encrypted under
                // the OLD key, defeating the entire point of rotation without any visible error.
                if (key is { Length: > 0 } && !_include(k))
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
        catch (MissingEnvFileSignal)
        {
            row.Error = new MissingEnvFileException(envFilepath);
        }
        catch (MissingEnvKeysFileSignal)
        {
            row.Error = new MissingEnvKeysFileException(localEnvKeysFilePath);
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

        // The historical rotate.js (commit f1ffd157) never populates unchangedFilepaths anywhere -
        // changedFilepaths.add(...) only runs on the success path, at the very end of the try
        // block, so a file that errors (missing .env/.env.keys, etc.) lands in NEITHER bucket -
        // it's tracked only via the per-file warning already logged from row.Error. Bucketing every
        // non-Changed row (including error rows, since row.Changed only ever becomes true on
        // success) into _unchangedFilePaths was a bug: it made a file that genuinely FAILED also
        // print a misleading "○ no change (thatfile)" summary line alongside its real warning,
        // something upstream never does. _unchangedFilePaths is therefore permanently empty here,
        // matching upstream's own (apparently unintentional) dead functionality exactly rather than
        // "fixing" it toward something that looks more complete.
        if (row.Changed)
        {
            _changedFilePaths.Add(envFilepath);
        }
    }

    private sealed class MissingEnvFileSignal : Exception;

    private sealed class MissingEnvKeysFileSignal : Exception;
}