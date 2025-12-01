using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/guess-public-key-name.test.js</remarks>
public class GuessPublicKeyNameTest
{
    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvReturnsDotenvPublicKey()
    {
        const string filepath = ".env";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNamePathToDotEnvReturnsDotenvPublicKey()
    {
        const string filepath = "some/path/to/.env";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvEnvReturnsDotenvPublicKeyEnv()
    {
        const string filepath = ".env.env";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_ENV");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvProductionReturnsDotenvPublicKeyProduction()
    {
        const string filepath = ".env.production";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_PRODUCTION");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvLocalReturnsDotenvPublicKeyLocal()
    {
        const string filepath = ".env.local";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_LOCAL");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvDevelopmentLocalReturnsDotenvPublicKeyDevelopmentLocal()
    {
        string filepath = ".env.development.local";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT_LOCAL");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvDevelopmentProductionReturnsDotenvPublicKeyDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT_PRODUCTION");
    }

    /// <remarks>Refer to "#guessPublicKeyName"</remarks>
    [Fact]
    public void TestGuessPublicKeyNameDotEnvSomeOtherThingReturnsDotenvPublicKeySomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string result = GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_SOME_OTHER");
    }
}