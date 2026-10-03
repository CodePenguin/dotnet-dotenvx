using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/validateEnvExample.test.js</remarks>
public class ValidateEnvExampleTest : EnvironmentTestBase
{
    /// <remarks>Refer to "validateEnvExample returns MISSING_ENV_EXAMPLE when .env.example is missing"</remarks>
    [Fact]
    public void TestMissingEnvExampleFile()
    {
        Fsx.ExistsSync = _ => false;

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string>());

        error.ShouldBeOfType<MissingEnvExampleException>();
        error!.MessageWithHelp.ShouldBe(
            "[MISSING_ENV_EXAMPLE] missing .env.example file. fix: [https://github.com/dotenvx/dotenvx/issues/905]");
    }

    /// <remarks>Refer to "validateEnvExample returns VALIDATION_FAILED when required keys are missing"</remarks>
    [Fact]
    public void TestMissingRequiredKey()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "DATABASE_URL=\nAPI_KEY=\nOPTIONAL= # optional\n";

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string>());

        error.ShouldBeOfType<ValidationFailedException>();
        error!.Code.ShouldBe("VALIDATION_FAILED");
        error.Message.ShouldBe("[VALIDATION_FAILED] missing required (DATABASE_URL, API_KEY)");
    }

    /// <remarks>Refer to "validateEnvExample returns undefined when valid"</remarks>
    [Fact]
    public void TestOptionalKeyExempted()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "DATABASE_URL=\nOPTIONAL= # optional\n";

        DotenvxException? error =
            ValidateEnvExample(new Dictionary<string, string> { { "DATABASE_URL", "postgres://localhost" } });

        error.ShouldBeNull();
    }

    /// <remarks>Not from a JS test - an empty env value counts as missing.</remarks>
    [Fact]
    public void TestEmptyValueTreatedAsMissing()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "HELLO=\n";

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string> { { "HELLO", "" } });

        error.ShouldBeOfType<ValidationFailedException>();
    }

    [Fact]
    public void TestAllKeysPresentValidates()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "HELLO=\nWORLD=\n";

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string>
        {
            { "HELLO", "value" }, { "WORLD", "value2" }
        });

        error.ShouldBeNull();
    }

    /// <remarks>
    /// Not from a JS test - JS's `\b` is ASCII-only (`/\boptional\b/i.test("optionalé")` is true),
    /// while .NET's default `\b` is Unicode-aware; see trap #43.
    /// </remarks>
    [Fact]
    public void OptionalCommentWordBoundaryIsAsciiOnly()
    {
        ValidationResult result = Validate(
            new Dictionary<string, List<string>> { ["KEY"] = [""] },
            new Dictionary<string, string>(),
            new Dictionary<string, List<string?>> { ["KEY"] = ["optionalé"] });

        result.Valid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
}
