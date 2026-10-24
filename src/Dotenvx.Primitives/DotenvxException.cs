using static Dotenvx.Primitives.Primitives;

namespace Dotenvx;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/errors.js. Every upstream error factory sets
/// `e.messageWithHelp = \`${message}. ${help}\`` and every CLI action prints
/// `logger.error(error.messageWithHelp || error.message)` - a single combined line, never
/// message and help as two separate log calls (found to be wrong here via a Node-vs-C# unit
/// test coverage audit, 2026-09-22 - every action's error-printing loop previously
/// re-implemented this inconsistently, several with entirely invented help text).
/// </remarks>
public class DotenvxException(string code, string message, string? help = null) : Exception(message)
{
    public string Code { get; private set; } = code;
    public string? Help { get; private set; } = help;

    // Matches errors.js's `Errors` constructor's `options.debug` - set on the rare error that
    // carries extra diagnostic detail beyond the user-facing message. No current call site in this
    // port sets it (upstream's own `debug` option isn't passed by any current call site either -
    // confirmed via grep of `.custom()` callers), but catchAndLog.js checks it unconditionally, so
    // CatchAndLog.cs needs somewhere to read it from.
    public string? Debug { get; init; }

    /// <summary>Attaches (or replaces) the help text after the fact (`error.help = ...; error.messageWithHelp = ...`).</summary>
    public void SetHelp(string help)
    {
        Help = help;
    }

    public string MessageWithHelp => string.IsNullOrEmpty(Help) ? Message : $"{Message}. {Help}";
}

public class InvalidConventionException(string convention) : DotenvxException(
    "INVALID_CONVENTION",
    $"[INVALID_CONVENTION] invalid convention ({convention})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/761]");

public class InvalidColorException(string color) : DotenvxException(
    "INVALID_COLOR",
    $"[INVALID_COLOR] Invalid color {color}",
    "fix: [must be 256 colors]");

public class CommandSubstitutionFailed(string key, string command, string message) : DotenvxException(
    "COMMAND_SUBSTITUTION_FAILED",
    $"[COMMAND_SUBSTITUTION_FAILED] could not eval {key} containing command '{command}': {message}",
    "fix: [https://github.com/dotenvx/dotenvx/issues/532]");

public class DecryptionFailedException : DotenvxException
{
    public DecryptionFailedException(string message, string? key = null) : base(
        "DECRYPTION_FAILED",
        $"[DECRYPTION_FAILED] {message}",
        "fix: [https://github.com/dotenvx/dotenvx/issues/757]")
    {
        Key = key;
    }

    // Set only by DecryptKeyValue.cs's per-key throw sites, so Run.cs's CoarsenDecryptErrors can
    // fold a directly-thrown DecryptionFailedException (e.g. malformed/empty ciphertext) into the
    // same combined "could not decrypt {keys}" message as the other four decrypt-failure
    // exceptions, instead of leaking its literal crypto-library message straight to the user.
    public string? Key { get; }
}

public class InvalidPrivateKeyException : DotenvxException
{
    public InvalidPrivateKeyException(string key, string privateKeyName, string privateKey) : base(
        "INVALID_PRIVATE_KEY",
        $"[INVALID_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'",
        "fix: [https://github.com/dotenvx/dotenvx/issues/465]")
    {
        Key = key;
    }

    // Exposed so a coarse-decrypt-error consumer (see Run.cs) can rebuild a generic
    // "could not decrypt {key}" DecryptionFailedException without re-parsing the message text -
    // matches @dotenvx/primitives' parse() coarsening every specific decrypt failure into
    // DECRYPTION_FAILED (confirmed live 2026-09-25), unlike main.js's own standalone parse()
    // function, which surfaces this specific code directly.
    public string Key { get; }
}

public class MalformedEncryptedDataException : DotenvxException
{
    public MalformedEncryptedDataException(string key) : base(
        "MALFORMED_ENCRYPTED_DATA",
        $"[MALFORMED_ENCRYPTED_DATA] could not decrypt {key} because encrypted data appears malformed",
        "fix: [https://github.com/dotenvx/dotenvx/issues/467]")
    {
        Key = key;
    }

    public string Key { get; }
}

public class MissingEnvFileException(string envFilePath = ".env") : DotenvxException(
    "MISSING_ENV_FILE",
    $"[MISSING_ENV_FILE] missing file ({envFilePath})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/484]");

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/errors.js's missingEnvKeysFile().
/// </remarks>
public class MissingEnvKeysFileException(string envKeysFilePath = ".env.keys") : DotenvxException(
    "MISSING_ENV_KEYS_FILE",
    $"[MISSING_ENV_KEYS_FILE] missing file ({envKeysFilePath})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/775]");

public class MissingPrivateKeyException : DotenvxException
{
    public MissingPrivateKeyException(string key, string privateKeyName, string privateKey) : base(
        "MISSING_PRIVATE_KEY",
        $"[MISSING_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'",
        "fix: [https://github.com/dotenvx/dotenvx/issues/464]")
    {
        Key = key;
    }

    public string Key { get; }
}

public class WrongPrivateKeyException : DotenvxException
{
    public WrongPrivateKeyException(string key, string privateKeyName, string privateKey) : base(
        "WRONG_PRIVATE_KEY",
        $"[WRONG_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'",
        "fix: [https://github.com/dotenvx/dotenvx/issues/466]")
    {
        Key = key;
    }

    public string Key { get; }
}

public class MissingKeyException(string key) : DotenvxException(
    "MISSING_KEY",
    $"[MISSING_KEY] missing key ({key})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/759]");

public class MissingValueException(string key) : DotenvxException(
    "MISSING_VALUE",
    $"[MISSING_VALUE] missing value ({key})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/864]");

public class MissingDirectoryException(string directory) : DotenvxException(
    "MISSING_DIRECTORY",
    $"[MISSING_DIRECTORY] missing directory ({directory})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/758]");

/// <remarks>
/// Confirmed reachable live, 2026-09-24 (revisiting an earlier "needs a decision" item that had
/// concluded this was unreachable) - a genuinely NEW file (no read ever attempted) in a directory
/// that denies file creation specifically (Windows: icacls /deny (WD,AD), distinct from denying
/// write on an EXISTING file's content) triggers this via `set`. The earlier "unreachable"
/// finding was based on testing an EXISTING write-denied file instead, where Windows denies the
/// READ too, so `setTransform`'s own read-phase catch swallows the raw error before the later
/// translated write call ever runs - a real, narrower reachability story than either "always
/// reachable" or "never reachable" would suggest.
/// </remarks>
public class FileNotWritableException(string filepath) : DotenvxException(
    "FILE_NOT_WRITABLE",
    $"[FILE_NOT_WRITABLE] cannot write to file ({filepath})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/890]");

public class MissingEnvFilesException() : DotenvxException(
    "MISSING_ENV_FILES",
    "[MISSING_ENV_FILES] no .env* files found",
    "fix: [https://github.com/dotenvx/dotenvx/issues/760]");

/// <remarks>
/// Mirrors errors.js's `.custom({message, help})` - used by prebuild.js/precommit.js for their
/// ad-hoc "not encrypted/dockerignored"-style messages, which never pass a `code` (confirmed by
/// reading every `.custom()` call site in both files). CatchAndLog.cs only logs an `ERROR_CODE:`
/// debug line when Code is non-empty, matching catchAndLog.js's `if (error.code)` check.
/// </remarks>
public class CustomException : DotenvxException
{
    /// <summary>errors.js ISSUE_BY_CODE: the text inside the generic `fix: [..]` help for each code.</summary>
    public static readonly IReadOnlyDictionary<string, string> IssueByCode = new Dictionary<string, string>
    {
        ["COMMAND_EXITED_WITH_CODE"] = "https://github.com/dotenvx/dotenvx/issues/new",
        ["COMMAND_SUBSTITUTION_FAILED"] = "https://github.com/dotenvx/dotenvx/issues/532",
        ["DECRYPTION_FAILED"] = "https://github.com/dotenvx/dotenvx/issues/757",
        ["INVALID_COLOR"] = "must be 256 colors",
        ["INVALID_CONVENTION"] = "https://github.com/dotenvx/dotenvx/issues/761",
        ["INVALID_PASSPHRASE"] = "try again with the correct passphrase",
        ["INVALID_PRIVATE_KEY"] = "https://github.com/dotenvx/dotenvx/issues/465",
        ["INVALID_PUBLIC_KEY"] = "https://github.com/dotenvx/dotenvx/issues/756",
        ["1PASSWORD_FAILED"] = "https://www.1password.dev/cli/get-started",
        ["BITWARDEN_FAILED"] = "https://bitwarden.com/help/cli/",
        ["MALFORMED_ENCRYPTED_DATA"] = "https://github.com/dotenvx/dotenvx/issues/467",
        ["MISPAIRED_PRIVATE_KEY"] = "https://github.com/dotenvx/dotenvx/issues/752",
        ["MISSING_DIRECTORY"] = "https://github.com/dotenvx/dotenvx/issues/758",
        ["MISSING_ENV_EXAMPLE"] = "https://github.com/dotenvx/dotenvx/issues/905",
        ["MISSING_ENV_FILE"] = "https://github.com/dotenvx/dotenvx/issues/484",
        ["MISSING_ENV_KEYS_FILE"] = "https://github.com/dotenvx/dotenvx/issues/775",
        ["MISSING_ENV_FILES"] = "https://github.com/dotenvx/dotenvx/issues/760",
        ["MISSING_KEY"] = "https://github.com/dotenvx/dotenvx/issues/759",
        ["MISSING_LOG_LEVEL"] = "must be valid log level",
        ["MISSING_PRIVATE_KEY"] = "https://github.com/dotenvx/dotenvx/issues/464",
        ["MISSING_PUBLIC_KEY"] = "https://github.com/dotenvx/dotenvx/issues/865",
        ["MISSING_VALUE"] = "https://github.com/dotenvx/dotenvx/issues/864",
        ["FILE_NOT_WRITABLE"] = "https://github.com/dotenvx/dotenvx/issues/890",
        ["PRECOMMIT_HOOK_MODIFY_FAILED"] = "try again or report error",
        ["VALIDATION_FAILED"] = "https://github.com/dotenvx/dotenvx/issues/907",
        ["WRONG_PRIVATE_KEY"] = "https://github.com/dotenvx/dotenvx/issues/466"
    };

    /// <summary>errors.js `custom()`: with a code and no explicit help, help is the generic `fix: [issue url]` for that code.</summary>
    public CustomException(string message, string? help = null, string? code = null, string? debug = null) : base(
        code ?? "",
        message,
        help ?? (code != null && IssueByCode.TryGetValue(code, out string? issue) ? $"fix: [{issue}]" : null))
    {
        Debug = debug;
    }
}

public class MissingPublicKeyException() : DotenvxException(
    "MISSING_PUBLIC_KEY",
    "[MISSING_PUBLIC_KEY] missing public key",
    "fix: [https://github.com/dotenvx/dotenvx/issues/865]");

public class PrecommitHookModifyFailedException(string originalMessage) : DotenvxException(
    "PRECOMMIT_HOOK_MODIFY_FAILED",
    $"[PRECOMMIT_HOOK_MODIFY_FAILED] failed to modify pre-commit hook: {originalMessage}",
    "fix: [try again or report error]");

/// <remarks>
/// Was "InvalidDotenvPrivateKeyException" with code "INVALID_DOTENV_PRIVATE_KEY" - neither the
/// class name nor the code exist upstream. The real upstream scenario/code is
/// errors.js's mispairedPrivateKey() / MISPAIRED_PRIVATE_KEY (a derived public key not matching
/// an existing one), which only needs publicKey/publicKeyExisting - found via the same audit.
/// </remarks>
public class MispairedPrivateKeyException(string publicKey, string publicKeyExisting) : DotenvxException(
    "MISPAIRED_PRIVATE_KEY",
    $"[MISPAIRED_PRIVATE_KEY] private key's derived public key ({Truncate(publicKey)}) does not match the existing public key ({Truncate(publicKeyExisting)})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/752]");

public class EnvfileRequiredException() : DotenvxException(
    "ENVFILE_REQUIRED",
    "[ENVFILE_REQUIRED] check requires an Envfile",
    "fix: run [dotenvx spec]");

/// <summary>errors.js `invalidEnv()`: the failed Envfile checks joined with "; ", without help.</summary>
public class InvalidEnvException(string message, IReadOnlyList<Dotenvx.Primitives.CheckError> diagnostics) : DotenvxException(
    "INVALID_ENV",
    $"[INVALID_ENV] {message}")
{
    public IReadOnlyList<Dotenvx.Primitives.CheckError> Diagnostics { get; } = diagnostics;
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/errors.js alreadyEncrypted() (`encrypt --with`). No `fix: [url]` help and no Code lookup, exactly as upstream.</remarks>
public class AlreadyEncryptedException(string envFilePath = ".env") : DotenvxException(
    "ALREADY_ENCRYPTED",
    $"[ALREADY_ENCRYPTED] {(string.IsNullOrEmpty(envFilePath) ? ".env" : envFilePath)} is already configured for encryption",
    "fix: use a fresh .env file");

/// <remarks>Reference source: dotenvx/src/lib/helpers/errors.js invalidPublicKey().</remarks>
public class InvalidPublicKeyException(string publicKeyName, string publicKey) : DotenvxException(
    "INVALID_PUBLIC_KEY",
    $"[INVALID_PUBLIC_KEY] could not encrypt using public key '{publicKeyName}={Truncate(publicKey)}'",
    "fix: [https://github.com/dotenvx/dotenvx/issues/756]");
