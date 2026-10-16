using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference sources: dotenvx/src/lib/envfile/init/*.js (`dotenvx spec`). Not from a JS test (upstream covers them through the
/// spec action); the expected Envfile text was produced by the real Node CLI from the same files.
/// </remarks>
[Collection("Fsx")]
public class EnvfileInitTest : EnvironmentTestBase
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-init-" + Guid.NewGuid().ToString("N"));

    public EnvfileInitTest()
    {
        Directory.CreateDirectory(_dir);
    }

    ~EnvfileInitTest()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private string P(string name) => Path.Combine(_dir, name);

    private void Write(string name, string content) => File.WriteAllText(P(name), content);

    private const string ExpectedFromExampleAndEnv =
        "# Envfile (safe to commit)\n# ------------------------\n\nstrict true\n\n" +
        "env \"EXTRA\"\nenv \"DATABASE_URL\", type: \"url\"\nenv \"PORT\", type: \"port\"\nenv \"SECRET\"\n";

    [Fact]
    public void InitWritesAnEnvfileFromTheExampleAndEnvFiles()
    {
        Write(".env", "DATABASE_URL=postgres://x\nPORT=3000\nDOTENV_PUBLIC_KEY=\"abc\"\nSECRET=hello\n");
        Write(".env.example", "EXTRA=1\n");

        InitEnvfileResult result = EnvfileInit.Init(_dir);

        result.Created.ShouldBeTrue();
        result.Replaced.ShouldBeFalse();
        File.ReadAllText(P("Envfile")).ShouldBe(ExpectedFromExampleAndEnv);
    }

    [Fact]
    public void InitToStdoutReturnsTheContentAndWritesNothing()
    {
        Write(".env", "DATABASE_URL=postgres://x\nPORT=3000\nSECRET=hello\n");
        Write(".env.example", "EXTRA=1\n");

        InitEnvfileResult result = EnvfileInit.Init(_dir, stdout: true);

        result.Content.ShouldNotBeNull().ShouldContain("env \"PORT\", type: \"port\"");
        File.Exists(P("Envfile")).ShouldBeFalse();
    }

    [Fact]
    public void InitLeavesAnExistingEnvfileAloneWithoutOverwrite()
    {
        Write(".env", "A=1\n");
        Write("Envfile", "custom\n");

        EnvfileInit.Init(_dir).Created.ShouldBeFalse();

        File.ReadAllText(P("Envfile")).ShouldBe("custom\n");
    }

    [Fact]
    public void InitReplacesAnExistingEnvfileWithOverwrite()
    {
        Write(".env", "A=1\n");
        Write("Envfile", "custom\n");

        InitEnvfileResult result = EnvfileInit.Init(_dir, overwrite: true);

        result.Created.ShouldBeTrue();
        result.Replaced.ShouldBeTrue();
        File.ReadAllText(P("Envfile")).ShouldContain("env \"A\"");
    }

    [Fact]
    public void InitRefusesToReplaceAnEnvfileThatIsADirectory()
    {
        Write(".env", "A=1\n");
        Directory.CreateDirectory(P("Envfile"));

        Should.Throw<Exception>(() => EnvfileInit.Init(_dir, overwrite: true)).Message
            .ShouldBe($"Cannot replace Envfile: expected a regular file ({P("Envfile")})");
    }

    [Fact]
    public void InitAddsKeysFoundInSourceCodeAfterTheEnvFiles()
    {
        Write(".env", "A=1\n");

        string content = EnvfileInit.Init(_dir, sourceKeys: ["FROM_CODE"], stdout: true).Content!;

        content.ShouldContain("env \"A\"");
        content.ShouldContain("# additionally found in code\nenv \"FROM_CODE\"");
    }

    [Theory]
    [InlineData("js", "const a = process.env.ALPHA; const b = process.env['BETA'];\n// process.env.NOPE", "ALPHA,BETA")]
    [InlineData("python", "os.environ['ONE']\nos.getenv(\"TWO\")\n# os.getenv('NO')", "ONE,TWO")]
    [InlineData("ruby", "ENV['ONE']\nENV.fetch('TWO')\n# ENV['NO']", "ONE,TWO")]
    [InlineData("go", "os.Getenv(\"ONE\")\n// os.Getenv(\"NO\")", "ONE")]
    [InlineData("csharp", "Environment.GetEnvironmentVariable(\"ONE\");\n// Environment.GetEnvironmentVariable(\"NO\")", "ONE")]
    public void ExtractEnvKeysReadsStaticAccessAndSkipsComments(string language, string content, string expected)
    {
        string.Join(',', EnvfileInit.ExtractEnvKeys(content, language)).ShouldBe(expected);
    }

    [Fact]
    public void DiscoverEnvFilesFindsDotenvFilesInTheDirectory()
    {
        Write(".env", "A=1\n");
        Write(".env.production", "A=2\n");
        Write("notes.txt", "x\n");

        List<string> found = EnvfileInit.DiscoverEnvFiles(_dir);

        found.Select(Path.GetFileName).ShouldContain(".env");
        found.Select(Path.GetFileName).ShouldContain(".env.production");
        found.Select(Path.GetFileName).ShouldNotContain("notes.txt");
    }
}
