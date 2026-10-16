using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference sources: dotenvx/src/lib/envfile/validation/diagnosticLocation.js and envfile/rendering/previewEnvfile.js. Not from a
/// JS test (upstream covers them through the `check` action); these exercise the location lookup and the preview table directly.
/// </remarks>
public class EnvfileDiagnosticsTest : EnvironmentTestBase
{
    private static RunProcessedEnv EnvFile(string path, string src, params string[] keys)
    {
        Dictionary<string, string> injected = keys.ToDictionary(key => key, _ => "v");

        return new RunProcessedEnv { Type = EnvType.EnvFile, FilePath = path, Src = src, Parsed = injected, Injected = injected };
    }

    private static string Here(string name) => Path.Combine(Directory.GetCurrentDirectory(), name);

    [Fact]
    public void LocationsPointToTheLineOfTheAssignment()
    {
        Func<string, string> location = EnvfileDiagnostics.Locations(
            [EnvFile(Here("a.env"), "# comment\nA=1\n\nexport B='x\ny'\nC: 3\n", "A", "B", "C")], "fallback");

        location("A").ShouldBe("a.env:2");
        location("B").ShouldBe("a.env:4");
        location("C").ShouldBe("a.env:6");
        location("UNKNOWN").ShouldBe("fallback");
    }

    [Fact]
    public void LocationsHandleCrLfLineEndings()
    {
        EnvfileDiagnostics.Locations([EnvFile(Here("a.env"), "A=1\r\nB=2\r\n", "A", "B")], "fallback")("B").ShouldBe("a.env:2");
    }

    [Fact]
    public void TheLastDefinitionWins()
    {
        EnvfileDiagnostics.Locations([EnvFile(Here("a.env"), "A=1\nA=2\n", "A")], "fallback")("A").ShouldBe("a.env:2");
    }

    [Fact]
    public void ShellValuesNameTheShellOrTheLabel()
    {
        var row = new RunProcessedEnv { Type = EnvType.Env, PreExisted = new Dictionary<string, string> { ["HOME"] = "x" } };

        EnvfileDiagnostics.Locations([row], "fallback")("HOME").ShouldBe("shell environment");
        EnvfileDiagnostics.Locations([row], "fallback", "process.env")("HOME").ShouldBe("process.env");
    }

    [Fact]
    public void EnvironmentValuesUseTheLabelWithoutALine()
    {
        var row = new RunProcessedEnv { Type = EnvType.Env, String = "A=1", Injected = new Dictionary<string, string> { ["A"] = "1" } };

        EnvfileDiagnostics.Locations([row], "fallback")("A").ShouldBe("--env:1");
        EnvfileDiagnostics.Locations([row], "fallback", "process.env")("A").ShouldBe("process.env");
    }

    [Fact]
    public void PreviewListsCheckedValuesWithTheirSources()
    {
        RunProcessedEnv row = EnvFile(Here(".env"), "A=1\nB=two words\nC=encrypted:abc\n", "A", "B", "C");
        var checkedValues = new Dictionary<string, string> { ["A"] = "1", ["B"] = "two words", ["C"] = "encrypted:abc" };

        string preview = EnvfileDiagnostics.Preview([row], EnvfileSchema.None, checkedValues, []);

        string[] lines = preview.Split('\n');
        lines.Length.ShouldBe(3);
        lines[0].ShouldContain("A=1");
        lines[0].ShouldContain("# .env:1 !declared");
        lines[1].ShouldContain("B=\"two words\"");
        lines[2].ShouldContain("C=[REDACTED]");
    }

    [Fact]
    public void PreviewMarksMissingKeysWithTheirErrorsAndHidesDotenvKeys()
    {
        RunProcessedEnv row = EnvFile(Here(".env"), "DOTENV_PUBLIC_KEY=x\nA=1\n", "DOTENV_PUBLIC_KEY", "A");
        var checkedValues = new Dictionary<string, string> { ["DOTENV_PUBLIC_KEY"] = "x", ["A"] = "1" };
        CheckError[] errors =
        [
            new("NEEDED", "MISSING", "required", "NEEDED is required", true),
            new("NEEDED", "MISSING", "required", "NEEDED is required", true),
            new("A", "INVALID", "", "A is invalid", true)
        ];

        string preview = EnvfileDiagnostics.Preview([row], EnvfileSchema.None, checkedValues, errors);

        preview.ShouldNotContain("DOTENV_PUBLIC_KEY");
        preview.ShouldContain("NEEDED=[MISSING]");
        preview.ShouldContain("!required");
        preview.ShouldContain("! A is invalid");
        preview.Split('\n').Count(line => line.Contains("NEEDED")).ShouldBe(1);
    }

    [Fact]
    public void PreviewOfNothingIsEmpty()
    {
        EnvfileDiagnostics.Preview([], EnvfileSchema.None, new Dictionary<string, string>(), []).ShouldBe("");
    }
}
