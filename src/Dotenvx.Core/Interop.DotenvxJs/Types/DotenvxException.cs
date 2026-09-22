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
