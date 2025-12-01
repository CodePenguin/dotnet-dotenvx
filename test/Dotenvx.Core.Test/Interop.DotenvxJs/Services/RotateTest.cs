using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/rotate.test.js</remarks>
public class RotateTest : EnvironmentTestBase
{
    public RotateTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "#run (no arguments)"</remarks>
    [Fact]
    public void TestRunNoArguments()
    {
        RotateResult result = new Rotate([]).Run();

        MissingEnvFileException exampleError = new(".env", Path.GetFullPath(".env"));

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([".env"]);
    }

    /// <remarks>Refer to "#run (no env file)"</remarks>
    [Fact]
    public void TestRunNoEnvFile()
    {
        RotateResult result = new Rotate([]).Run();

        MissingEnvFileException exampleError = new(".env", Path.GetFullPath(".env"));

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.Type.ShouldBe(EnvType.EnvFile);
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        p.EnvFilePath.ShouldBe(".env");
        p.Error.ShouldBeEquivalentTo(exampleError);

        result.ChangedFilePaths.ShouldBeEmpty();
        result.UnchangedFilePaths.ShouldBe([".env"]);
    }

    /// <remarks>Refer to "#run (no arguments and some other error)"</remarks>
    [Fact]
    public void TestRunNoArgumentsAndSomeOtherError()
    {
        Fsx.ReadFileX = (_, _) => throw new Exception("Mock Error");
        Fsx.ReadFileSync = _ => []; // Default to utf8 for DetectEncoding

        RotateResult result = new Rotate([]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Error.ShouldBeOfType<Exception>();
        p.Error.Message.ShouldStartWith("Mock Error");
        p.EnvFilePath.ShouldBe(".env");
        p.FilePath.ShouldBe(Path.GetFullPath(".env"));
        result.ChangedFilePaths.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (finds .env file)"</remarks>
    [Fact]
    public void TestRunFindsEnvFile()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file with specified glob string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileWithSpecifiedGlobString()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, ["H*"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKey()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key as string)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKeyAsString()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, ["HELLO"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file excluding specified key globbed)"</remarks>
    [Fact]
    public void TestRunFindsEnvFileExcludingSpecifiedKeyGlobbed()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, ["HE*"]).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBeEmpty();
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }

    /// <remarks>Refer to "#run (finds .env file) and custom envKeysFilepath"</remarks>
    [Fact]
    public void TestRunFindsEnvFileAndCustomEnvKeysFilepath()
    {
        const string envFile = "tests/monorepo/apps/encrypted/.env";
        const string envKeysFile = "tests/monorepo/apps/encrypted/.env.keys";

        Dictionary<string, object> originalParsed = DotenvParse(Fsx.ReadFileX(envFile));
        Dictionary<string, object> originalKeysParsed = DotenvParse(Fsx.ReadFileX(envKeysFile));

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = envFile }];

        RotateResult result = new Rotate(envs, null, null, envKeysFile).Run();

        result.ProcessedEnvs.ShouldHaveSingleItem();
        RotateProcessedEnv p = result.ProcessedEnvs[0];
        p.Keys.ShouldBe(["HELLO"]);
        p.EnvFilePath.ShouldBe(envFile);
        result.ChangedFilePaths.ShouldBe([envFile]);
        result.UnchangedFilePaths.ShouldBeEmpty();

        Dictionary<string, object> parsed = DotenvParse(p.EnvSrc);

        parsed.Keys.ShouldBe(["DOTENV_PUBLIC_KEY", "HELLO"]);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBeNullOrEmpty();
        (parsed["HELLO"] as string).ShouldStartWith("encrypted:");

        Dictionary<string, object> parsedKeys = DotenvParse(p.EnvKeysSrc);
        parsedKeys.Keys.ShouldBe(["DOTENV_PRIVATE_KEY"]);

        (parsed["HELLO"] as string).ShouldNotBe(originalParsed["HELLO"] as string);
        (parsed["DOTENV_PUBLIC_KEY"] as string).ShouldNotBe(originalParsed["DOTENV_PUBLIC_KEY"] as string);
        (parsedKeys["DOTENV_PRIVATE_KEY"] as string).ShouldNotBe(originalKeysParsed["DOTENV_PRIVATE_KEY"] as string);
    }
}