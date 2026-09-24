using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/validateEnvExample.js</remarks>
public class ValidateEnvExampleTest : EnvironmentTestBase
{
    [Fact]
    public void TestMissingEnvExampleFile()
    {
        Fsx.ExistsSync = _ => false;

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string>());

        error.ShouldBeOfType<MissingEnvExampleException>();
        error!.MessageWithHelp.ShouldBe(
            "[MISSING_ENV_EXAMPLE] missing .env.example file. fix: [https://github.com/dotenvx/dotenvx/issues/905]");
    }

    [Fact]
    public void TestMissingRequiredKey()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "HELLO=\nWORLD=\n";

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string> { { "HELLO", "value" } });

        error.ShouldBeOfType<ValidationFailedException>();
        error!.MessageWithHelp.ShouldBe(
            "[VALIDATION_FAILED] missing required (WORLD). fix: [https://github.com/dotenvx/dotenvx/issues/907]");
    }

    [Fact]
    public void TestOptionalKeyExempted()
    {
        Fsx.ExistsSync = _ => true;
        Fsx.ReadFileX = (_, _) => "HELLO=\nWORLD= # optional\n";

        DotenvxException? error = ValidateEnvExample(new Dictionary<string, string> { { "HELLO", "value" } });

        error.ShouldBeNull();
    }

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
}
