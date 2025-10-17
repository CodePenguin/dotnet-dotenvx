using Dotenvx.Core.Interop.Dotenvx;
using Dotenvx.Core.Interop.Dotenvx.Types;

using Shouldly;

namespace Dotenvx.Test.Interop.Dotenvx.Helpers;

public class ConfigTest : EnvironmentTestBase
{
    private const string EnvFileName = "./.env";
    private const string EnvLocalFileName = "./.env.local";

    [Fact]
    public void TakesArrayForPathOption()
    {
        ConfigOptions options = new() { Path = [EnvFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    [Fact]
    public void TakesTwoOrMoreFilesAndFirstOneWins()
    {
        ConfigOptions options = new() { Path = [EnvLocalFileName, EnvFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("local_basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");
    }

    [Fact]
    public void DoesNotWriteOverKeysAlreadyInProcessEnv()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");
        ConfigOptions options = new() { Path = [EnvFileName] };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("existing");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("existing");
    }

    [Fact]
    public void WritesOverKeysAlreadyInProcessEnvIfOverloadTrue()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");
        ConfigOptions options = new() { Path = [EnvFileName], Overload = true };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }

    [Fact]
    public void CanWriteToADifferentObjectThanProcessEnv()
    {
        Dictionary<string, string> myObject = new();
        // This will be used by the parser for variable expansion, but not written to
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = myObject };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        myObject["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBeNull();
    }

    [Fact]
    public void ReturnsParsedObject()
    {
        ConfigOptions options = new() { Path = [EnvFileName] };
        ConfigResult result = Main.Config(options);

        result.Error.ShouldBeNull();
        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void ReturnsErrorWhenFileMissing()
    {
        ConfigOptions options = new() { Path = ["nonexistent.env"] };
        ConfigResult result = Main.Config(options);
        result.Error.ShouldNotBeNull();
        result.Error.ShouldBeOfType<MissingEnvFileException>();
    }

    [Fact]
    public void SetsValuesFromBothEnvLocalAndEnvWhenBothExist()
    {
        // In this scenario, .env.local is first, so its values should take precedence.
        ConfigOptions options = new() { Path = [EnvLocalFileName, EnvFileName] };
        ConfigResult result = Main.Config(options);

        // from .env.local (first file wins)
        result.Parsed["BASIC"].ShouldBe("local_basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("local_basic");

        // from .env.local only
        result.Parsed["LOCAL"].ShouldBe("local");
        Environment.GetEnvironmentVariable("LOCAL").ShouldBe("local");

        // from .env only
        result.Parsed["SINGLE_QUOTES"].ShouldBe("single_quotes");
        Environment.GetEnvironmentVariable("SINGLE_QUOTES").ShouldBe("single_quotes");
    }

    [Fact]
    public void DoesNotOverrideExistingFalsyValues()
    {
        Environment.SetEnvironmentVariable("BASIC", "");
        ConfigOptions options = new() { Path = [EnvFileName] };
        ConfigResult result = Main.Config(options);

        /*
        Node.js behavior is to not override blank values but .NET 8.0 removes the environment variables when set to blank
        Expected behavior changes after .NET 9.0 https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable
        */
        const string expected = "basic"; // Will be string.Empty in .NET 9.0
        result.Parsed["BASIC"].ShouldBe(expected);
        Environment.GetEnvironmentVariable("BASIC").ShouldBe(expected);
    }

    [Fact]
    public void DoesOverrideExistingFalsyValuesIfOverloadTrue()
    {
        Environment.SetEnvironmentVariable("BASIC", "");
        ConfigOptions options = new() { Path = [EnvFileName], Overload = true };
        ConfigResult result = Main.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        Environment.GetEnvironmentVariable("BASIC").ShouldBe("basic");
    }
}