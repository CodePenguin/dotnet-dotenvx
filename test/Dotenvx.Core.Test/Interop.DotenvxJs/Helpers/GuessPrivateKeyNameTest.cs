using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/conventions/keynames.js, test titles from
/// dotenvx/tests/lib/conventions/keynames.test.js (only the privateKeyName half - see
/// GuessPublicKeyNameTest.cs for the publicKeyName half of the same JS test cases).
/// guessPrivateKeyName.test.js/guessPrivateKeyName.js no longer exist at this pin - moved into
/// conventions/keynames.js during the custodian/keyring reorg. Kept as extra coverage since the
/// behavior is still valid, but they don't map to a current upstream test title 1:1 - see each
/// remark.
/// </remarks>
public class GuessPrivateKeyNameTest
{
    /// <remarks>Refer to "keynames returns base key names for .env"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvReturnsDotenvPrivateKey()
    {
        const string filepath = ".env";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPrivateKeyName.test.js's "#guessPrivateKeyName (.env)".</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameSomePathToDotEnvReturnsDotenvPrivateKey()
    {
        const string filepath = "some/path/to/.env";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPrivateKeyName.test.js's "#guessPrivateKeyName (.env.env)".</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvEnvReturnsDotenvPrivateKeyEnv()
    {
        const string filepath = ".env.env";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_ENV");
    }

    /// <remarks>Refer to "keynames returns environment key names for .env.production"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvProductionReturnsDotenvPrivateKeyProduction()
    {
        const string filepath = ".env.production";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_PRODUCTION");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPrivateKeyName.test.js's "#guessPrivateKeyName (.env.local)".</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvLocalReturnsDotenvPrivateKeyLocal()
    {
        const string filepath = ".env.local";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_LOCAL");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPrivateKeyName.test.js's "#guessPrivateKeyName (.env.development.local)".</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvDevelopmentLocalReturnsDotenvPrivateKeyDevelopmentLocal()
    {
        const string filepath = ".env.development.local";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_DEVELOPMENT_LOCAL");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPrivateKeyName.test.js's "#guessPrivateKeyName (.env.development.production)".</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvDevelopmentProductionReturnsDotenvPrivateKeyDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_DEVELOPMENT_PRODUCTION");
    }

    /// <remarks>No longer a distinct current upstream scenario - historical coverage from the pre-reorg guessPrivateKeyName.test.js's "#guessPrivateKeyName (.env.some.other.thing)". Superseded in spirit by "keynames truncates long environment suffixes" below.</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvSomeOtherThingReturnsDotenvPrivateKeySomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_SOME_OTHER");
    }

    /// <remarks>Refer to "keynames returns local environment key names"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvCiLocalReturnsDotenvPrivateKeyCiLocal()
    {
        const string filepath = ".env.ci.local";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_CI_LOCAL");
    }

    /// <remarks>Refer to "keynames handles .env1"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnv1ReturnsDotenvPrivateKeyDevelopment1()
    {
        const string filepath = ".env1";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_DEVELOPMENT1");
    }

    /// <remarks>Refer to "keynames truncates long environment suffixes"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvCiLocalExtraTruncatesToCiLocal()
    {
        const string filepath = ".env.ci.local.extra";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_CI_LOCAL");
    }

    /// <remarks>
    /// Refer to "keynames handles .env.txt". Verified against the real Node CLI: `.env.txt`
    /// derives the plain DOTENV_PRIVATE_KEY, not DOTENV_PRIVATE_KEY_TXT - see the matching fix/
    /// remarks in GuessPublicKeyNameTest.cs. Real, shipped bug, not just a test gap.
    /// </remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvTxtStripsExtensionReturnsDotenvPrivateKey()
    {
        const string filepath = ".env.txt";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY");
    }

    /// <remarks>Refer to "keynames handles .env.production.txt"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvProductionTxtReturnsDotenvPrivateKeyProduction()
    {
        const string filepath = ".env.production.txt";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_PRODUCTION");
    }

    /// <remarks>Refer to "keynames handles uppercase filenames"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameUppercaseFilenameReturnsDotenvPrivateKeyLocal()
    {
        const string filepath = ".ENV.LOCAL";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_LOCAL");
    }
}
