namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class DotenvxException(string code, string message, string? help = null) : Exception(message)
{
    public string Code { get; private set; } = code;
    public string? Help { get; private set; } = help;
}

public class CommandSubstitutionFailed(string key, string command, string message) : DotenvxException(
    "COMMAND_SUBSTITUTION_FAILED",
    $"[COMMAND_SUBSTITUTION_FAILED] could not eval {key} containing command '{command}': {message}",
    "[COMMAND_SUBSTITUTION_FAILED] https://github.com/dotenvx/dotenvx/issues/532");

public class DecryptionFailedException(string message) : DotenvxException(
    "DECRYPTION_FAILED",
    message,
    "[DECRYPTION_FAILED] Run with debug flag [dotenvx run --debug -- yourcommand] or manually run [echo $DOTENV_KEY] to compare it to the one in .env.keys.");

public class InvalidPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "INVALID_PRIVATE_KEY",
    $"[INVALID_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Helpers.Helpers.Truncate(privateKey)}'",
    "[INVALID_PRIVATE_KEY] https://github.com/dotenvx/dotenvx/issues/465");

public class MalformedEncryptedDataException(string key) : DotenvxException(
    "MALFORMED_ENCRYPTED_DATA",
    $"[MALFORMED_ENCRYPTED_DATA] could not decrypt {key} because encrypted data appears malformed",
    "[MALFORMED_ENCRYPTED_DATA] https://github.com/dotenvx/dotenvx/issues/467");

public class MissingEnvFileException(string envFilepath, string filepath) : DotenvxException(
    "MISSING_ENV_FILE",
    $"[MISSING_ENV_FILE] missing {envFilepath} file ({filepath})",
    "[MISSING_ENV_FILE] https://github.com/dotenvx/dotenvx/issues/484");

public class MissingPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "MISSING_PRIVATE_KEY",
    $"[MISSING_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Helpers.Helpers.Truncate(privateKey)}'",
    "[MISSING_PRIVATE_KEY] https://github.com/dotenvx/dotenvx/issues/464");

public class WrongPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "WRONG_PRIVATE_KEY",
    $"[WRONG_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Helpers.Helpers.Truncate(privateKey)}'",
    "[WRONG_PRIVATE_KEY] https://github.com/dotenvx/dotenvx/issues/466");

public class MissingKeyException(string key) : DotenvxException(
    "MISSING_KEY",
    $"[MISSING_KEY] missing {key} key");

public class InvalidDotenvPrivateKeyException(string publicKey, string existingPublicKey) : DotenvxException(
    "INVALID_DOTENV_PRIVATE_KEY",
    $"derived public key ({Helpers.Helpers.Truncate(publicKey)}) does not match the existing public key ({Helpers.Helpers.Truncate(existingPublicKey)})");