using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/conventions/keynames.js, test titles from
/// dotenvx/tests/lib/conventions/keynames.test.js (only the publicKeyName half - see
/// GuessPrivateKeyNameTest.cs for the privateKeyName half of the same JS test cases).
/// guess-public-key-name.test.js/guessPublicKeyName.js (the old titles below still reference)
/// no longer exist at this pin - moved into conventions/keynames.js during the custodian/
/// keyring reorg. Kept as extra coverage since the behavior is still valid, but they don't map
/// to a current upstream test title 1:1 - see each remark.
/// </remarks>
public class GuessPublicKeyNameTest
{
    /// <remarks>Refer to "keynames returns base key names for .env"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvReturnsDotenvPublicKey()
    {
        const string filepath = ".env";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPublicKeyName.test.js's "#guessPublicKeyName".</remarks>
    [Fact]
    public void TestGuessPublicKeyNamePathToDotEnvReturnsDotenvPublicKey()
    {
        const string filepath = "some/path/to/.env";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPublicKeyName.test.js's "#guessPublicKeyName".</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvEnvReturnsDotenvPublicKeyEnv()
    {
        const string filepath = ".env.env";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_ENV");
    }

    /// <remarks>Refer to "keynames returns environment key names for .env.production"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvProductionReturnsDotenvPublicKeyProduction()
    {
        const string filepath = ".env.production";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_PRODUCTION");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPublicKeyName.test.js's "#guessPublicKeyName".</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvLocalReturnsDotenvPublicKeyLocal()
    {
        const string filepath = ".env.local";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_LOCAL");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPublicKeyName.test.js's "#guessPublicKeyName".</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvDevelopmentLocalReturnsDotenvPublicKeyDevelopmentLocal()
    {
        string filepath = ".env.development.local";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT_LOCAL");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPublicKeyName.test.js's "#guessPublicKeyName".</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvDevelopmentProductionReturnsDotenvPublicKeyDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT_PRODUCTION");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPublicKeyName.test.js's "#guessPublicKeyName". Superseded in spirit by "keynames truncates long environment suffixes" below.</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvSomeOtherThingReturnsDotenvPublicKeySomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_SOME_OTHER");
    }

    /// <remarks>
    /// Refer to "keynames returns local environment key names". Verified live via Node - a real
    /// gap this exact case would have caught (found via a Node-vs-C# unit test coverage audit,
    /// 2026-09-22, after which .env.local-style historical tests above were confirmed to still pass).
    /// </remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvCiLocalReturnsDotenvPublicKeyCiLocal()
    {
        const string filepath = ".env.ci.local";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_CI_LOCAL");
    }

    /// <remarks>Refer to "keynames handles .env1"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnv1ReturnsDotenvPublicKeyDevelopment1()
    {
        const string filepath = ".env1";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT1");
    }

    /// <remarks>Refer to "keynames truncates long environment suffixes"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvCiLocalExtraTruncatesToCiLocal()
    {
        const string filepath = ".env.ci.local.extra";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_CI_LOCAL");
    }

    /// <remarks>
    /// Refer to "keynames handles .env.txt". Verified against the real Node CLI: `.env.txt`
    /// derives the plain DOTENV_PUBLIC_KEY, not DOTENV_PUBLIC_KEY_TXT -
    /// dotenvx/src/lib/conventions/keynames.js strips a trailing .txt via canonicalEnvFilename.js
    /// before deriving the name. Our port previously missed this entirely (found via a Node-vs-C#
    /// unit test coverage audit, 2026-09-22) - this was a real, shipped bug, not just a test gap.
    /// </remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvTxtStripsExtensionReturnsDotenvPublicKey()
    {
        const string filepath = ".env.txt";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    /// <remarks>Refer to "keynames handles .env.production.txt". Same fix as above, with an environment suffix present alongside the stripped .txt.</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvProductionTxtReturnsDotenvPublicKeyProduction()
    {
        const string filepath = ".env.production.txt";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_PRODUCTION");
    }

    /// <remarks>Refer to "keynames handles uppercase filenames"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameUppercaseFilenameReturnsDotenvPublicKeyLocal()
    {
        const string filepath = ".ENV.LOCAL";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_LOCAL");
    }
}
