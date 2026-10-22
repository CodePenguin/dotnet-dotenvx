using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{sanitizeCommandForMetadata,keypairMetadata,removeDynamicHelpSection,
/// removeOptionsHelpParts,isTeamRequiredError}.js
/// </remarks>
public class MetadataHelpersTest : EnvironmentTestBase
{
    /// <remarks>Refer to "sanitizeCommandForMetadata redacts token flag values"</remarks>
    [Fact]
    public void TestSanitizeCommandForMetadataRedactsTokenFlagValues()
    {
        SanitizeCommandForMetadata.Sanitize(["keypair", "--token", "token-123", "-f", ".env.production"])
            .ShouldBe("keypair --token [REDACTED] -f .env.production");
        SanitizeCommandForMetadata.Sanitize(["keypair", "--token=token-123", "-f", ".env.production"])
            .ShouldBe("keypair --token=[REDACTED] -f .env.production");
        SanitizeCommandForMetadata.Sanitize("keypair --token token-123 -f .env.production")
            .ShouldBe("keypair --token [REDACTED] -f .env.production");
    }

    /// <remarks>Refer to "sanitizeCommandForMetadata redacts common secret flag values"</remarks>
    [Fact]
    public void TestSanitizeCommandForMetadataRedactsCommonSecretFlagValues()
    {
        SanitizeCommandForMetadata.Sanitize(["login", "--password", "super-secret", "--api-key=api-key-123", "--private-key", "private-key-123"])
            .ShouldBe("login --password [REDACTED] --api-key=[REDACTED] --private-key [REDACTED]");
        SanitizeCommandForMetadata.Sanitize(["login", "--client-secret", "secret-123", "--refresh-token=refresh-123"])
            .ShouldBe("login --client-secret [REDACTED] --refresh-token=[REDACTED]");
        SanitizeCommandForMetadata.Sanitize(["login", "--passphrase", "secret words", "-f", ".env.production"])
            .ShouldBe("login --passphrase [REDACTED] -f .env.production");
    }

    /// <remarks>Refer to "sanitizeCommandForMetadata parses quoted string command values before redacting"</remarks>
    [Fact]
    public void TestSanitizeCommandForMetadataParsesQuotedStringCommandValuesBeforeRedacting()
    {
        SanitizeCommandForMetadata.Sanitize("login --password \"secret words\" --api-key api-key-123 -f .env.production")
            .ShouldBe("login --password [REDACTED] --api-key [REDACTED] -f .env.production");
        SanitizeCommandForMetadata.Sanitize("login --private-key 'private key words' --team dotenvx")
            .ShouldBe("login --private-key [REDACTED] --team dotenvx");
    }

    private string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "dotenvx-keypair-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.CurrentDirectory = dir;

        return dir;
    }

    /// <remarks>Refer to "keypairMetadata includes file context and command string from metadata json"</remarks>
    [Fact]
    public void TestKeypairMetadataIncludesFileContextAndCommandStringFromMetadataJson()
    {
        string dir = TempDir();
        File.WriteAllText(Path.Combine(dir, ".env.production"), "HELLO=World\nDATABASE_URL=postgres://localhost/db");
        // not inside a git repository: the project is the working directory
        SecretCli.Exec = (_, _, _) => new SecretCliResult(128, "");

        Dictionary<string, object> result = KeypairMetadata.Get(".env.production",
            """{"command":"dotenvx run -f .env.production -- npm start"}""");

        result["filepath"].ShouldBe(".env.production");
        result["filename"].ShouldBe(".env.production");
        result["environment"].ShouldBe("production");
        result["project_name"].ShouldBe(Path.GetFileName(dir));
        ((string[])result["keys"]).ShouldBe(["HELLO", "DATABASE_URL"]);
        result["command"].ShouldBe("dotenvx run -f .env.production -- npm start");
    }

    /// <remarks>Refer to "keypairMetadata omits command for invalid metadata json"</remarks>
    [Fact]
    public void TestKeypairMetadataOmitsCommandForInvalidMetadataJson()
    {
        TempDir();
        SecretCli.Exec = (_, _, _) => new SecretCliResult(128, "");

        Dictionary<string, object> result = KeypairMetadata.Get(".env.missing", "not json");

        result.ContainsKey("command").ShouldBeFalse("does not include malformed command metadata");
        result["filename"].ShouldBe(".env.missing");
        result["environment"].ShouldBe("missing");
    }

    /// <remarks>Refer to "keypairMetadata redacts token values from command metadata"</remarks>
    [Fact]
    public void TestKeypairMetadataRedactsTokenValuesFromCommandMetadata()
    {
        TempDir();
        SecretCli.Exec = (_, _, _) => new SecretCliResult(128, "");

        Dictionary<string, object> result = KeypairMetadata.Get(".env.missing",
            """{"command":"dotenvx keypair --token token-123 -f .env.production"}""");

        result["command"].ShouldBe("dotenvx keypair --token [REDACTED] -f .env.production");
    }

    /// <remarks>Refer to "keypairMetadata reads key names without decrypting env values"</remarks>
    [Fact]
    public void TestKeypairMetadataReadsKeyNamesWithoutDecryptingEnvValues()
    {
        string dir = TempDir();
        File.WriteAllText(Path.Combine(dir, ".env"), "DOTENV_PUBLIC_KEY=\"03abc\"\nHELLO=\"encrypted:abc123\"");
        SecretCli.Exec = (_, _, _) => new SecretCliResult(128, "");

        Dictionary<string, object> result = KeypairMetadata.Get(".env");

        ((string[])result["keys"]).ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
    }

    /// <remarks>Refer to "#removeDynamicHelpSection"</remarks>
    [Fact]
    public void TestRemoveDynamicHelpSection()
    {
        List<string> lines =
        [
            "Usage: dotenvx [options] [command] [command] [args...]", "", "a secure dotenv–from the creator of `dotenv`", "",
            "Arguments:", "  command                      dynamic command", "  args                         dynamic command arguments",
            "", "Options:"
        ];

        HelpTextFilters.RemoveDynamicHelpSection(lines);

        lines.ShouldBe(
        [
            "Usage: dotenvx [options] [command] [command] [args...]", "", "a secure dotenv–from the creator of `dotenv`", "",
            "Options:"
        ]);
    }

    /// <remarks>Refer to "#removeOptionsHelpParts"</remarks>
    [Fact]
    public void TestRemoveOptionsHelpParts()
    {
        List<string> lines = ["set [options] <KEY> <value>  set a single environment variable"];

        HelpTextFilters.RemoveOptionsHelpParts(lines);

        lines.ShouldBe(["set <KEY> <value>  set a single environment variable"]);
    }

    /// <remarks>Refer to "#removeOptionsHelpParts aligns command descriptions after [options] removal"</remarks>
    [Fact]
    public void TestRemoveOptionsHelpPartsAlignsCommandDescriptionsAfterOptionsRemoval()
    {
        List<string> lines =
        [
            "Commands:", "  run [options]              inject env at runtime",
            "  get [KEY] [options]        return a single environment variable",
            "  login [options]            login via dotenvx-armor", "", "Advanced:"
        ];

        HelpTextFilters.RemoveOptionsHelpParts(lines);

        lines.ShouldBe(
        [
            "Commands:", "  run        inject env at runtime", "  get [KEY]  return a single environment variable",
            "  login      login via dotenvx-armor", "", "Advanced:"
        ]);
    }

    /// <remarks>Refer to "#removeOptionsHelpParts hides the Options section"</remarks>
    [Fact]
    public void TestRemoveOptionsHelpPartsHidesTheOptionsSection()
    {
        List<string> lines =
        [
            "Usage: dotenvx run -- yourcommand", "", "Options:",
            "  -l, --log-level <level>  set log level (default: \"info\")", "  -q, --quiet              sets log level to error",
            "  -v, --verbose            sets log level to verbose", "  -d, --debug              sets log level to debug",
            "  -V, --version            output the version number", "  -h, --help               display help for command", "",
            "Commands:", "  run [options]  inject env at runtime"
        ];

        HelpTextFilters.RemoveOptionsHelpParts(lines);

        lines.ShouldBe(["Usage: dotenvx run -- yourcommand", "", "Commands:", "  run  inject env at runtime"]);
    }

    /// <remarks>Reference source: dotenvx/tests/lib/helpers/isTeamRequiredError.test.js (assertions without titles).</remarks>
    [Fact]
    public void TestIsTeamRequiredError()
    {
        ArmorTeam.IsTeamRequiredError(new global::Dotenvx.ApiException("TEAM_REQUIRED", "x")).ShouldBeTrue();
        ArmorTeam.IsTeamRequiredError(new global::Dotenvx.ApiException("DOTENVX_TEAM_REQUIRED", "x")).ShouldBeTrue();
        ArmorTeam.IsTeamRequiredError(new global::Dotenvx.ApiException("PERMISSION_DENIED", "x")).ShouldBeFalse();
        ArmorTeam.IsTeamRequiredError(null).ShouldBeFalse();
    }
}
