using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/errors.test.js. The C# errors are typed exception classes (one per
/// errors.js factory) instead of one `Errors` class, so each upstream assertion block maps onto the matching
/// exception. The 1Password and Bitwarden factories are `CustomException`s with those codes.
/// </remarks>
public class ErrorsTest
{
    /// <remarks>Refer to "#errors onePasswordFailed"</remarks>
    [Fact]
    public void TestOnePasswordFailed()
    {
        CustomException result = new("[1PASSWORD_FAILED] 1Password CLI failed to resolve API_KEY", code: "1PASSWORD_FAILED");

        result.Code.ShouldBe("1PASSWORD_FAILED");
        result.Message.ShouldBe("[1PASSWORD_FAILED] 1Password CLI failed to resolve API_KEY");
        result.Help.ShouldBe("fix: [https://www.1password.dev/cli/get-started]");
        result.MessageWithHelp.ShouldBe(
            "[1PASSWORD_FAILED] 1Password CLI failed to resolve API_KEY. fix: [https://www.1password.dev/cli/get-started]");
    }

    /// <remarks>Refer to "#errors bitwardenFailed"</remarks>
    [Fact]
    public void TestBitwardenFailed()
    {
        CustomException result = new("[BITWARDEN_FAILED] Bitwarden Password Manager CLI failed to resolve API_KEY",
            code: "BITWARDEN_FAILED");

        result.Code.ShouldBe("BITWARDEN_FAILED");
        result.Message.ShouldBe("[BITWARDEN_FAILED] Bitwarden Password Manager CLI failed to resolve API_KEY");
        result.Help.ShouldBe("fix: [https://bitwarden.com/help/cli/]");
        result.MessageWithHelp.ShouldBe(
            "[BITWARDEN_FAILED] Bitwarden Password Manager CLI failed to resolve API_KEY. fix: [https://bitwarden.com/help/cli/]");
    }

    /// <remarks>Refer to "#errors custom with code auto-fix and debug"</remarks>
    [Fact]
    public void TestCustomWithCodeAutoFixAndDebug()
    {
        CustomException result = new("boom", code: "MISSING_ENV_FILE", debug: "trace");

        result.Code.ShouldBe("MISSING_ENV_FILE");
        result.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/484]");
        result.MessageWithHelp.ShouldBe("boom. fix: [https://github.com/dotenvx/dotenvx/issues/484]");
        result.Debug.ShouldBe("trace");
    }

    /// <remarks>Refer to "#errors custom with explicit help and no code". An absent JS `code` is an empty code here.</remarks>
    [Fact]
    public void TestCustomWithExplicitHelpAndNoCode()
    {
        CustomException result = new("boom", "custom help");

        result.Code.ShouldBe("");
        result.Help.ShouldBe("custom help");
        result.MessageWithHelp.ShouldBe("boom. custom help");
    }

    /// <remarks>Refer to "#errors missingEnvFile falls back to .env"</remarks>
    [Fact]
    public void TestMissingEnvFileFallsBackToEnv()
    {
        MissingEnvFileException result = new();

        result.Code.ShouldBe("MISSING_ENV_FILE");
        result.Message.ShouldBe("[MISSING_ENV_FILE] missing file (.env)");
        result.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/484]");
        result.MessageWithHelp.ShouldBe(
            "[MISSING_ENV_FILE] missing file (.env). fix: [https://github.com/dotenvx/dotenvx/issues/484]");
    }

    /// <remarks>Refer to "#errors missingEnvKeysFile falls back to .env.keys"</remarks>
    [Fact]
    public void TestMissingEnvKeysFileFallsBackToEnvKeys()
    {
        MissingEnvKeysFileException result = new();

        result.Code.ShouldBe("MISSING_ENV_KEYS_FILE");
        result.Message.ShouldBe("[MISSING_ENV_KEYS_FILE] missing file (.env.keys)");
        result.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/775]");
        result.MessageWithHelp.ShouldBe(
            "[MISSING_ENV_KEYS_FILE] missing file (.env.keys). fix: [https://github.com/dotenvx/dotenvx/issues/775]");
    }

    /// <remarks>Refer to "#errors missingPublicKey"</remarks>
    [Fact]
    public void TestMissingPublicKey()
    {
        MissingPublicKeyException result = new();

        result.Code.ShouldBe("MISSING_PUBLIC_KEY");
        result.Message.ShouldBe("[MISSING_PUBLIC_KEY] missing public key");
        result.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/865]");
        result.MessageWithHelp.ShouldBe(
            "[MISSING_PUBLIC_KEY] missing public key. fix: [https://github.com/dotenvx/dotenvx/issues/865]");
    }

    /// <remarks>Refer to "#errors fileNotWritable"</remarks>
    [Fact]
    public void TestFileNotWritable()
    {
        const string filepath = "C:\\Windows\\System32\\.env.keys";

        FileNotWritableException result = new(filepath);

        result.Code.ShouldBe("FILE_NOT_WRITABLE");
        result.Message.ShouldBe($"[FILE_NOT_WRITABLE] cannot write to file ({filepath})");
        result.Help.ShouldBe("fix: [https://github.com/dotenvx/dotenvx/issues/890]");
        result.MessageWithHelp.ShouldBe(
            $"[FILE_NOT_WRITABLE] cannot write to file ({filepath}). fix: [https://github.com/dotenvx/dotenvx/issues/890]");
    }
}
