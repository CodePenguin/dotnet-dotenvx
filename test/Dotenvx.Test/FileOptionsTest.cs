using System.CommandLine;

using Dotenvx.Actions;
using Dotenvx.Core.Test;

using Shouldly;

namespace dotnet_dotenvx.Test;

/// <remarks>
/// Reference source: dotenvx/tests/cli/commands/fileOptions.test.js. Upstream's `configureFileOptions` adds `--file` and the
/// hidden `--env-file` legacy spelling to every `-f, --env-file` option; here each command declares the option with all three
/// names, so the same checks run against a variadic command (`check`) and a scalar one (`native up`).
/// </remarks>
public class FileOptionsTest : EnvironmentTestBase
{
    private static string[] Files(ParseResult parse)
    {
        Option<string[]?> option = (Option<string[]?>)parse.CommandResult.Command.Options.Single(o => o.Name == "--file");

        return parse.GetValue(option) ?? [];
    }

    /// <remarks>Refer to "file spellings share the original attribute and repeated value parsing"</remarks>
    [Theory]
    [InlineData("--file")]
    [InlineData("--env-file")]
    [InlineData("-f")]
    public void TestFileSpellingsShareTheOriginalAttribute(string flag)
    {
        var command = new CheckAction(MockLogger, new global::Dotenvx.Logging.DotenvxLoggerOptions());

        Files(command.Parse([flag, ".env.production"])).ShouldBe([".env.production"]);
    }

    /// <remarks>Refer to "file spellings share the original attribute and repeated value parsing" (repeated spellings)</remarks>
    [Fact]
    public void TestRepeatedSpellingsAccumulate()
    {
        var command = new CheckAction(MockLogger, new global::Dotenvx.Logging.DotenvxLoggerOptions());

        Files(command.Parse(["--file=.env.a", "--env-file", ".env.b", "-f", ".env.c"])).ShouldBe([".env.a", ".env.b", ".env.c"]);
        command.Options.Single(o => o.Name == "--file").Aliases.ShouldBe(["-f", "--env-file"], ignoreOrder: true);
    }

    /// <remarks>Refer to "nested commands retain scalar and variadic options and defaults"</remarks>
    [Fact]
    public void TestNestedCommandsRetainScalarAndVariadicOptionsAndDefaults()
    {
        var native = new NativeCommand(MockLogger);
        Command up = native.Subcommands.Single(c => c.Name == "up");
        Option<string?> file = (Option<string?>)up.Options.Single(o => o.Name == "--file");
        Option<string> keys = (Option<string>)up.Options.Single(o => o.Name == "--env-keys-file");

        up.Parse([]).GetValue(file).ShouldBeNull();
        up.Parse([]).GetValue(keys).ShouldBe(".env.keys");
        up.Parse(["--file", ".env.production"]).GetValue(file).ShouldBe(".env.production");
        up.Parse(["--env-file", ".env.production"]).GetValue(file).ShouldBe(".env.production");
    }
}
