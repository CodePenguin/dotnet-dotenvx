using Dotenvx.Core.Interop.Dotenvx.Types;

using Shouldly;

namespace Dotenvx.Test.Interop.Dotenvx.Helpers;

public class ConfigTest
{
    private const string EnvFileName = "./.env";
    private const string EnvLocalFileName = "./.env.local";

    [Fact]
    public void TakesStringForPathOption()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = processEnv };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        processEnv["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TakesArrayForPathOption()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = processEnv };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        processEnv["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TakesTwoOrMoreFilesAndFirstOneWins()
    {
        Dictionary<string, string> processEnv = new();
        ConfigOptions options = new() { Path = [EnvLocalFileName, EnvFileName], ProcessEnv = processEnv };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("local_basic");
        processEnv["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void DoesNotWriteOverKeysAlreadyInProcessEnv()
    {
        Dictionary<string, string> processEnv = new() { { "BASIC", "existing" } };
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = processEnv };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("existing");
        processEnv["BASIC"].ShouldBe("existing");
    }

    [Fact]
    public void WritesOverKeysAlreadyInProcessEnvIfOverrideTrue()
    {
        Dictionary<string, string> processEnv = new() { { "BASIC", "existing" } };
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = processEnv, Override = true };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        processEnv["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void CanWriteToADifferentObjectThanProcessEnv()
    {
        Dictionary<string, string> myObject = new();
        Dictionary<string, string> processEnv = new() { { "BASIC", "other" } }; // This will be used by the parser for variable expansion, but not written to
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = myObject };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        myObject["BASIC"].ShouldBe("basic");
        processEnv.ShouldNotContainKey("BASIC");
    }

    [Fact]
    public void ReturnsParsedObject()
    {
        ConfigOptions options = new() { Path = [EnvFileName] };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Error.ShouldBeNull();
        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void ReturnsErrorWhenFileMissing()
    {
        ConfigOptions options = new() { Path = ["nonexistent.env"] };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);
        result.Error.ShouldNotBeNull();
        result.Error.ShouldBeOfType<MissingEnvFileException>();
    }

    [Fact]
    public void SetsValuesFromBothEnvLocalAndEnvWhenBothExist()
    {
        Dictionary<string, string> processEnv = new();
        // In this scenario, .env.local is first, so its values should take precedence.
        ConfigOptions options = new() { Path = [EnvLocalFileName, EnvFileName], ProcessEnv = processEnv };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        // from .env.local (first file wins)
        result.Parsed["BASIC"].ShouldBe("local_basic");
        processEnv["BASIC"].ShouldBe("local_basic");

        // from .env.local only
        result.Parsed["LOCAL"].ShouldBe("local");
        processEnv["LOCAL"].ShouldBe("local");

        // from .env only
        result.Parsed["SINGLE_QUOTES"].ShouldBe("single_quotes");
        processEnv["SINGLE_QUOTES"].ShouldBe("single_quotes");
    }

    [Fact]
    public void DoesNotOverrideExistingFalsyValues()
    {
        Dictionary<string, string> processEnv = new() { { "BASIC", "" } };
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = processEnv };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("");
        processEnv["BASIC"].ShouldBe("");
    }

    [Fact]
    public void DoesOverrideExistingFalsyValuesIfOverrideTrue()
    {
        Dictionary<string, string> processEnv = new() { { "BASIC", "" } };
        ConfigOptions options = new() { Path = [EnvFileName], ProcessEnv = processEnv, Override = true };
        ConfigResult result = Core.Interop.Dotenvx.Helpers.Dotenvx.Config(options);

        result.Parsed["BASIC"].ShouldBe("basic");
        processEnv["BASIC"].ShouldBe("basic");
    }
}