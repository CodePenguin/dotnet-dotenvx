using Shouldly;

using DotenvxHelpers = Dotenvx.Core.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Helpers;

public class GuessEnvironmentTest
{
    [Fact]
    public void GuessEnvironment_DotEnv_ReturnsDevelopment()
    {
        const string filepath = ".env";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("development");
    }

    [Fact]
    public void GuessEnvironment_DotEnvProduction_ReturnsProduction()
    {
        const string filepath = ".env.production";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("production");
    }

    [Fact]
    public void GuessEnvironment_DotEnvLocal_ReturnsLocal()
    {
        const string filepath = ".env.local";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("local");
    }

    [Fact]
    public void GuessEnvironment_DotEnvDevelopmentLocal_ReturnsDevelopmentLocal()
    {
        const string filepath = ".env.development.local";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("development_local");
    }

    [Fact]
    public void GuessEnvironment_DotEnvDevelopmentProduction_ReturnsDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("development_production");
    }

    [Fact]
    public void GuessEnvironment_DotEnvSomeOtherThing_ReturnsSomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("some_other");
    }

    [Fact]
    public void GuessEnvironment_DotEnv1_ReturnsDevelopment1()
    {
        const string filepath = ".env1";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("development1");
    }

    [Fact]
    public void GuessEnvironment_SecretsTxt_ReturnsSecretsTxt()
    {
        const string filepath = "secrets.txt";
        string environment = DotenvxHelpers.GuessEnvironment(filepath);

        environment.ShouldBe("secrets.txt");
    }
}