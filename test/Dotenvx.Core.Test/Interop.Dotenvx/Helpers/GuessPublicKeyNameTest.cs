using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

public class GuessPublicKeyNameTest
{
    [Fact]
    public void GuessPublicKeyName_DotEnv_ReturnsDotenvPublicKey()
    {
        const string filepath = ".env";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    [Fact]
    public void GuessPublicKeyName_PathToDotEnv_ReturnsDotenvPublicKey()
    {
        const string filepath = "some/path/to/.env";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY");
    }

    [Fact]
    public void GuessPublicKeyName_DotEnvEnv_ReturnsDotenvPublicKeyEnv()
    {
        const string filepath = ".env.env";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_ENV");
    }

    [Fact]
    public void GuessPublicKeyName_DotEnvProduction_ReturnsDotenvPublicKeyProduction()
    {
        const string filepath = ".env.production";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_PRODUCTION");
    }

    [Fact]
    public void GuessPublicKeyName_DotEnvLocal_ReturnsDotenvPublicKeyLocal()
    {
        const string filepath = ".env.local";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_LOCAL");
    }

    [Fact]
    public void GuessPublicKeyName_DotEnvDevelopmentLocal_ReturnsDotenvPublicKeyDevelopmentLocal()
    {
        string filepath = ".env.development.local";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT_LOCAL");
    }

    [Fact]
    public void GuessPublicKeyName_DotEnvDevelopmentProduction_ReturnsDotenvPublicKeyDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_DEVELOPMENT_PRODUCTION");
    }

    [Fact]
    public void GuessPublicKeyName_DotEnvSomeOtherThing_ReturnsDotenvPublicKeySomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string result = DotenvxHelpers.GuessPublicKeyName(filepath);

        result.ShouldBe("DOTENV_PUBLIC_KEY_SOME_OTHER");
    }
}