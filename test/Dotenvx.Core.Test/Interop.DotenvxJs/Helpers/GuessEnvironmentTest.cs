using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/conventions/environment.js, tests from
/// dotenvx/tests/lib/conventions/environment.test.js (moved there from
/// dotenvx/tests/lib/helpers/guess-environment.test.js during the custodian/keyring reorg -
/// found stale via a Node-vs-C# unit test coverage audit, 2026-09-22 - the test titles
/// themselves are unchanged by the move, just the file location).
/// </remarks>
public class GuessEnvironmentTest
{
    /// <remarks>Refer to "#environment (.env)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvReturnsDevelopment()
    {
        const string filepath = ".env";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development");
    }

    /// <remarks>Refer to "#environment (.env.production)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvProductionReturnsProduction()
    {
        const string filepath = ".env.production";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("production");
    }

    /// <remarks>Refer to "#environment (.env.local)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvLocalReturnsLocal()
    {
        const string filepath = ".env.local";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("local");
    }

    /// <remarks>Refer to "#environment (.env.development.local)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvDevelopmentLocalReturnsDevelopmentLocal()
    {
        const string filepath = ".env.development.local";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development_local");
    }

    /// <remarks>Refer to "#environment (.env.development.production)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvDevelopmentProductionReturnsDevelopmentProduction()
    {
        const string filepath = ".env.development.production";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development_production");
    }

    /// <remarks>Refer to "#environment (.env.some.other.thing)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvSomeOtherThingReturnsSomeOther()
    {
        const string filepath = ".env.some.other.thing";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("some_other");
    }

    /// <remarks>Refer to "#environment (.env1)"</remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnv1ReturnsDevelopment1()
    {
        const string filepath = ".env1";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development1");
    }

    /// <remarks>Refer to "#environment (secrets.txt)"</remarks>
    [Fact]
    public void TestGuessEnvironmentSecretsTxtReturnsSecretsTxt()
    {
        const string filepath = "secrets.txt";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("secrets.txt");
    }

    /// <remarks>
    /// Refer to "#environment (.env.txt)". environment.js routes through canonicalEnvFilename.js,
    /// which strips a trailing .txt from a `.env*` filename before deriving the environment -
    /// `.env.txt` canonicalizes to `.env`, which has no environment segment at all, so this
    /// resolves the same way plain ".env" does ("development"), not "txt" or "development_txt".
    /// </remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvTxtReturnsDevelopment()
    {
        const string filepath = ".env.txt";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("development");
    }

    /// <remarks>
    /// Refer to "#environment (.env.production.txt)". `.env.production.txt` must resolve to
    /// "production", not "production_txt" - found via a Node-vs-C# unit test coverage audit,
    /// 2026-09-22, alongside the matching real bug in GuessPublicKeyName/GuessPrivateKeyName.
    /// </remarks>
    [Fact]
    public void TestGuessEnvironmentDotEnvProductionTxtStripsExtension()
    {
        const string filepath = ".env.production.txt";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("production");
    }

    /// <remarks>Refer to "#environment (.ENV.PRODUCTION)"</remarks>
    [Fact]
    public void TestGuessEnvironmentUppercaseReturnsProduction()
    {
        const string filepath = ".ENV.PRODUCTION";
        string environment = GuessEnvironment(filepath);

        environment.ShouldBe("production");
    }
}
