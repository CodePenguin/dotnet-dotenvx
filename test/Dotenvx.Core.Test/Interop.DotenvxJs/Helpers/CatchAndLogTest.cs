using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/catchAndLog.test.js</remarks>
public class CatchAndLogTest : EnvironmentTestBase
{
    /// <remarks>Refer to "catchAndLog - WRONG_PRIVATE_KEY"</remarks>
    [Fact]
    public void TestCatchAndLogWrongPrivateKey()
    {
        DotenvxException error = new("WRONG_PRIVATE_KEY",
            "[WRONG_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=199bdd6…'",
            "fix: [https://github.com/dotenvx/dotenvx/issues/466]") { Debug = "debug details" };

        CatchAndLog(MockLogger, error);

        MockLogger.ErrorMessages.ShouldContain(
            "[WRONG_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY=199bdd6…'. fix: [https://github.com/dotenvx/dotenvx/issues/466]");
        MockLogger.InfoMessages.ShouldNotContain(s => s.Contains("fix:"));
        MockLogger.DebugMessages.ShouldContain("debug details");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: WRONG_PRIVATE_KEY");
    }

    /// <remarks>Refer to "catchAndLog - MISSING_PRIVATE_KEY"</remarks>
    [Fact]
    public void TestCatchAndLogMissingPrivateKey()
    {
        DotenvxException error = new("MISSING_PRIVATE_KEY",
            "[MISSING_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY='",
            "fix: [https://github.com/dotenvx/dotenvx/issues/464]");

        CatchAndLog(MockLogger, error);

        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_PRIVATE_KEY] could not decrypt HELLO using private key 'DOTENV_PRIVATE_KEY='. fix: [https://github.com/dotenvx/dotenvx/issues/464]");
        MockLogger.InfoMessages.ShouldNotContain(s => s.Contains("fix:"));
    }

    /// <remarks>
    /// Refer to "catchAndLog - INVALID_PUBLIC_KEY". Uses the base DotenvxException directly - no
    /// InvalidPublicKeyException subclass exists in this port, since errors.js's
    /// invalidPublicKey() factory has no confirmed call site anywhere in dotenvx/src at this pin
    /// (grepped, only the JS test file references it) - not worth adding an unused exception type
    /// just for this test.
    /// </remarks>
    [Fact]
    public void TestCatchAndLogInvalidPublicKey()
    {
        DotenvxException error = new("INVALID_PUBLIC_KEY",
            "[INVALID_PUBLIC_KEY] could not encrypt using public key 'DOTENV_PUBLIC_KEY=10248e9…'",
            "fix: [https://github.com/dotenvx/dotenvx/issues/756]");

        CatchAndLog(MockLogger, error);

        MockLogger.ErrorMessages.ShouldContain(
            "[INVALID_PUBLIC_KEY] could not encrypt using public key 'DOTENV_PUBLIC_KEY=10248e9…'. fix: [https://github.com/dotenvx/dotenvx/issues/756]");
        MockLogger.InfoMessages.ShouldNotContain(s => s.Contains("fix:"));
    }

    /// <remarks>Refer to "catchAndLog - other error with help/debug"</remarks>
    [Fact]
    public void TestCatchAndLogOtherErrorWithHelpAndDebug()
    {
        DotenvxException error = new("OTHER_ERROR", "boom", "help text") { Debug = "debug text" };

        CatchAndLog(MockLogger, error);

        MockLogger.ErrorMessages.ShouldContain("boom. help text");
        MockLogger.InfoMessages.ShouldNotContain("help text");
        MockLogger.DebugMessages.ShouldContain("debug text");
        MockLogger.DebugMessages.ShouldContain("ERROR_CODE: OTHER_ERROR");
    }

    /// <remarks>
    /// Adapted from "catchAndLog preserves filesystem permission errors" - upstream's JS test
    /// hand-sets a mutable `.code = 'EPERM'` string property directly on a plain Error object,
    /// which .NET's IOException has no equivalent for (no public, settable string "code" - just
    /// HResult, an int). There is currently no code path in this port that produces a
    /// DotenvxException for a raw OS-level permission error (confirmed via
    /// TestEncryptOtherError/TestDecryptOtherError/TestSetOtherError in the *ActionTest.cs files,
    /// which lock in the raw OS exception message passing straight through unmodified - matching
    /// live-verified real Node CLI behavior for the one CLI action checked, `set`, at this pin).
    /// This test instead verifies the adjacent, still-meaningful invariant: a plain
    /// non-DotenvxException Exception (which is what an unhandled IOException/UnauthorizedAccessException
    /// actually is in this port) logs its own Message untranslated and does NOT emit an ERROR_CODE
    /// debug line (since there's no Code to read).
    /// </remarks>
    [Fact]
    public void TestCatchAndLogPreservesPlainExceptionsWithoutErrorCode()
    {
        Exception error = new("EPERM: operation not permitted, open 'C:\\Windows\\System32\\.env.keys'");

        CatchAndLog(MockLogger, error);

        MockLogger.ErrorMessages.ShouldContain(
            "EPERM: operation not permitted, open 'C:\\Windows\\System32\\.env.keys'");
        MockLogger.DebugMessages.ShouldNotContain(s => s.StartsWith("ERROR_CODE:"));
    }

    /// <remarks>Refer to "catchAndLog keeps trailing periods"</remarks>
    [Fact]
    public void TestCatchAndLogKeepsTrailingPeriods()
    {
        DotenvxException wrongPrivateKey = new("WRONG_PRIVATE_KEY", "[WRONG_PRIVATE_KEY] could not decrypt",
            "fix: [https://github.com/dotenvx/dotenvx/issues/466]");
        DotenvxException missingPrivateKey = new("MISSING_PRIVATE_KEY", "[MISSING_PRIVATE_KEY] could not decrypt",
            "fix: [https://github.com/dotenvx/dotenvx/issues/464]");
        DotenvxException invalidPublicKey = new("INVALID_PUBLIC_KEY", "[INVALID_PUBLIC_KEY] could not encrypt",
            "fix: [https://github.com/dotenvx/dotenvx/issues/756]");

        CatchAndLog(MockLogger, wrongPrivateKey);
        CatchAndLog(MockLogger, missingPrivateKey);
        CatchAndLog(MockLogger, invalidPublicKey);

        MockLogger.ErrorMessages.ShouldContain(
            "[WRONG_PRIVATE_KEY] could not decrypt. fix: [https://github.com/dotenvx/dotenvx/issues/466]");
        MockLogger.ErrorMessages.ShouldContain(
            "[MISSING_PRIVATE_KEY] could not decrypt. fix: [https://github.com/dotenvx/dotenvx/issues/464]");
        MockLogger.ErrorMessages.ShouldContain(
            "[INVALID_PUBLIC_KEY] could not encrypt. fix: [https://github.com/dotenvx/dotenvx/issues/756]");
        MockLogger.InfoMessages.ShouldNotContain(s => s.Contains("fix:"));
    }
}
