using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Types;

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

    public string MessageWithHelp => string.IsNullOrEmpty(Help) ? Message : $"{Message}. {Help}";
}

public class CommandSubstitutionFailed(string key, string command, string message) : DotenvxException(
    "COMMAND_SUBSTITUTION_FAILED",
    $"[COMMAND_SUBSTITUTION_FAILED] could not eval {key} containing command '{command}': {message}",
    "fix: [https://github.com/dotenvx/dotenvx/issues/532]");

public class DecryptionFailedException(string message) : DotenvxException(
    "DECRYPTION_FAILED",
    $"[DECRYPTION_FAILED] {message}",
    "fix: [https://github.com/dotenvx/dotenvx/issues/757]");

public class InvalidPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "INVALID_PRIVATE_KEY",
    $"[INVALID_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'",
    "fix: [https://github.com/dotenvx/dotenvx/issues/465]");

public class MalformedEncryptedDataException(string key) : DotenvxException(
    "MALFORMED_ENCRYPTED_DATA",
    $"[MALFORMED_ENCRYPTED_DATA] could not decrypt {key} because encrypted data appears malformed",
    "fix: [https://github.com/dotenvx/dotenvx/issues/467]");

public class MissingEnvFileException(string envFilePath) : DotenvxException(
    "MISSING_ENV_FILE",
    $"[MISSING_ENV_FILE] missing file ({envFilePath})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/484]");

public class MissingPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "MISSING_PRIVATE_KEY",
    $"[MISSING_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'",
    "fix: [https://github.com/dotenvx/dotenvx/issues/464]");

public class WrongPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "WRONG_PRIVATE_KEY",
    $"[WRONG_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'",
    "fix: [https://github.com/dotenvx/dotenvx/issues/466]");

public class MissingKeyException(string key) : DotenvxException(
    "MISSING_KEY",
    $"[MISSING_KEY] missing key ({key})",
    "fix: [https://github.com/dotenvx/dotenvx/issues/759]");

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

public class MissingEnvExampleException() : DotenvxException(
    "MISSING_ENV_EXAMPLE",
    "[MISSING_ENV_EXAMPLE] missing .env.example file",
    "fix: [https://github.com/dotenvx/dotenvx/issues/905]");

public class ValidationFailedException(string message) : DotenvxException(
    "VALIDATION_FAILED",
    $"[VALIDATION_FAILED] {message}",
    "fix: [https://github.com/dotenvx/dotenvx/issues/907]");

/// <remarks>
/// Mirrors errors.js's `.custom({message, help})` - used by prebuild.js/precommit.js for their
/// ad-hoc "not encrypted/dockerignored"-style messages, which never pass a `code` (confirmed by
/// reading every `.custom()` call site in both files). CatchAndLog.cs only logs an `ERROR_CODE:`
/// debug line when Code is non-empty, matching catchAndLog.js's `if (error.code)` check.
/// </remarks>
public class CustomException(string message, string help) : DotenvxException(
    "",
    message,
    help);

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
