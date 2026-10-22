using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/guessPrivateKeyFilename.test.js</remarks>
public class GuessPrivateKeyFilenameTest
{
    /// <remarks>Refer to "#guessPrivateKeyFilename (DOTENV_PRIVATE_KEY)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyFilenameDotenvPrivateKeyReturnsDotEnv()
    {
        string result = GuessPrivateKeyFilename("DOTENV_PRIVATE_KEY");

        result.ShouldBe(".env");
    }

    /// <remarks>Refer to "#guessPrivateKeyFilename (DOTENV_PRIVATE_KEY_PRODUCTION)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyFilenameDotenvPrivateKeyProductionReturnsDotEnvProduction()
    {
        string result = GuessPrivateKeyFilename("DOTENV_PRIVATE_KEY_PRODUCTION");

        result.ShouldBe(".env.production");
    }

    /// <remarks>Refer to "#guessPrivateKeyFilename (DOTENV_PRIVATE_KEY_CI)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyFilenameDotenvPrivateKeyCiReturnsDotEnvCi()
    {
        string result = GuessPrivateKeyFilename("DOTENV_PRIVATE_KEY_CI");

        result.ShouldBe(".env.ci");
    }

    /// <remarks>Refer to "#guessPrivateKeyFilename (DOTENV_PRIVATE_KEY_DEVELOPMENT_LOCAL)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyFilenameDotenvPrivateKeyDevelopmentLocalReturnsDotEnvDevelopmentLocal()
    {
        string result = GuessPrivateKeyFilename("DOTENV_PRIVATE_KEY_DEVELOPMENT_LOCAL");

        result.ShouldBe(".env.development.local");
    }

    /// <remarks>Refer to "#guessPrivateKeyFilename (DOTENV_PRIVATE_KEY_DEVELOPMENT_LOCAL_ME)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyFilenameDotenvPrivateKeyDevelopmentLocalMeReturnsDotEnvDevelopmentLocalMe()
    {
        string result = GuessPrivateKeyFilename("DOTENV_PRIVATE_KEY_DEVELOPMENT_LOCAL_ME");

        result.ShouldBe(".env.development.local.me");
    }
}
