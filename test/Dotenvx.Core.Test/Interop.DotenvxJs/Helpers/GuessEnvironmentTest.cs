using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/guess-environment.test.js</remarks>
public class GuessEnvironmentTest
{
    /// <remarks>Refer to "#guessEnvironment (.env)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvReturnsDevelopment()
    {
        const string filepath = ".env";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development");
    }

    /// <remarks>Refer to "#guessEnvironment (.env.production)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvProductionReturnsProduction()
    {
        const string filepath = ".env.production";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("production");
    }

    /// <remarks>Refer to "#guessEnvironment (.env.local)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvLocalReturnsLocal()
    {
        const string filepath = ".env.local";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("local");
    }

    /// <remarks>Refer to "#guessEnvironment (.env.development.local)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvDevelopmentLocalReturnsDevelopmentLocal()
    {
        const string filepath = ".env.development.local";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development_local");
    }

    /// <remarks>Refer to "#guessEnvironment (.env.development.production)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvDevelopmentProductionReturnsDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development_production");
    }

    /// <remarks>Refer to "#guessEnvironment (.env.some.other.thing)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvSomeOtherThingReturnsSomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("some_other");
    }

    /// <remarks>Refer to "#guessEnvironment (.env1)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnv1ReturnsDevelopment1()
    {
        const string filepath = ".env1";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development1");
    }

    /// <remarks>Refer to "#guessEnvironment (secrets.txt)"</remarks>
    [Fact]
    public void TestGuessEnvironmentSecretsTxtReturnsSecretsTxt()
    {
        const string filepath = "secrets.txt";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("secrets.txt");
    }
}