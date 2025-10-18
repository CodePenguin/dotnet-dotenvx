namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class DotenvxException(string code, string message) : Exception(message)
{
    public string Code { get; private set; } = code;

    protected static string Truncate(string? value, int showCharacters = 7)
    {
        return value is { Length: > 0 }
            ? value[..showCharacters] + "…"
            : string.Empty;
    }
}

public class CommandSubstitutionFailed(string key, string command, string message) : DotenvxException(
    "COMMAND_SUBSTITUTION_FAILED",
    $"[COMMAND_SUBSTITUTION_FAILED] could not eval {key} containing command '{command}': {message}");

public class DecryptionFailedException(string message) : DotenvxException("DECRYPTION_FAILED", message);

public class InvalidPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "INVALID_PRIVATE_KEY",
    $"[INVALID_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'");

public class MalformedEncryptedDataException(string key) : DotenvxException("MALFORMED_ENCRYPTED_DATA",
    $"[MALFORMED_ENCRYPTED_DATA] could not decrypt {key} because encrypted data appears malformed");

public class MissingEnvFileException(string envFilepath, string filepath) : DotenvxException("MISSING_ENV_FILE",
    $"[MISSING_ENV_FILE] missing {envFilepath} file ({filepath})");

public class MissingPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "MISSING_PRIVATE_KEY",
    $"[MISSING_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'");

public class WrongPrivateKeyException(string key, string privateKeyName, string privateKey) : DotenvxException(
    "WRONG_PRIVATE_KEY",
    $"[WRONG_PRIVATE_KEY] could not decrypt {key} using private key '{privateKeyName}={Truncate(privateKey)}'");