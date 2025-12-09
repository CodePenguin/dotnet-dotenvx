using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/guessPrivateKeyName.test.js</remarks>
public class GuessPrivateKeyNameTest
{
    /// <remarks>Refer to "#guessPrivateKeyName (.env)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvReturnsDotenvPrivateKey()
    {
        const string filepath = ".env";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameSomePathToDotEnvReturnsDotenvPrivateKey()
    {
        const string filepath = "some/path/to/.env";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env.env)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvEnvReturnsDotenvPrivateKeyEnv()
    {
        const string filepath = ".env.env";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_ENV");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env.production)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvProductionReturnsDotenvPrivateKeyProduction()
    {
        const string filepath = ".env.production";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_PRODUCTION");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env.local)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvLocalReturnsDotenvPrivateKeyLocal()
    {
        const string filepath = ".env.local";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_LOCAL");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env.development.local)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvDevelopmentLocalReturnsDotenvPrivateKeyDevelopmentLocal()
    {
        const string filepath = ".env.development.local";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_DEVELOPMENT_LOCAL");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env.development.production)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvDevelopmentProductionReturnsDotenvPrivateKeyDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_DEVELOPMENT_PRODUCTION");
    }

    /// <remarks>Refer to "#guessPrivateKeyName (.env.some.other.thing)"</remarks>
    [Fact]
    public void TestGuessPrivateKeyNameDotEnvSomeOtherThingReturnsDotenvPrivateKeySomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string result = GuessPrivateKeyName(filepath);

        result.ShouldBe("DOTENV_PRIVATE_KEY_SOME_OTHER");
    }
}