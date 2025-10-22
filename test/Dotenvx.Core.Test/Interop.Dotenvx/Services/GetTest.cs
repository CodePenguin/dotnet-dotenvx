using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.Dotenvx.Services;

public class GetTest : EnvironmentTestBase
{
    public GetTest()
    {
        ClearEnvironment();
    }

    [Fact]
    public void RunMissingKeyReturnsTheEntireProcessEnvAsObject()
    {
        GetResult result = new Get(null, [], false, false, null).Run();

        result.Parsed.ShouldBeEmpty();
    }

    [Fact]
    public void RunAllObjectWithPresetProcessEnv()
    {
        Environment.SetEnvironmentVariable("PRESET_ENV_EXAMPLE", "something/on/machine");

        GetResult result = new Get(null, [], false, true, "").Run();
        result.Parsed.ShouldBe(new Dictionary<string, string> { ["PRESET_ENV_EXAMPLE"] = "something/on/machine" });

        result = new Get(null, [], false, false, "").Run();
        result.Parsed.ShouldBeEmpty();
    }

    [Fact]
    public void RunMissingKeyReturnsTheEntireProcessEnvAsObjectWithEnvFiles()
    {
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get(null, envs, false, false, null).Run();

        result.Parsed.ShouldBe(new Dictionary<string, string> { ["BASIC"] = "local_basic", ["LOCAL"] = "local" });
    }

    [Fact]
    public void RunMissingKeyReturnsEmptyStringWhenFetchingSingleKey()
    {
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get("BAZ", envs, false, false, null).Run();

        result.Parsed.ShouldNotContainKey("BAZ");
    }

    [Fact]
    public void Run()
    {
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env" }];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void RunAsMultiArray()
    {
        List<EnvItem> envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }
        ];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void RunAsMultiArrayReversedFirstWins()
    {
        List<EnvItem> envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env.local" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env" }
        ];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("local_basic");
    }

    [Fact]
    public void RunAsMultiArrayReversedWithOverloadSecondWins()
    {
        List<EnvItem> envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env.local" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env" }
        ];
        GetResult result = new Get("BASIC", envs, true, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void RunAsMultiArraySomeNotFound()
    {
        List<EnvItem> envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env.notfound" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env" }
        ];
        GetResult result = new Get("BASIC", envs, true, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void RunProcessEnvAlreadyExistsOnMachine()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");

        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("existing");
    }

    [Fact]
    public void RunNoKeyAndProcessEnvAlreadyExistsOnMachine()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");

        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get(null, envs, false, false, null).Run();

        result.Parsed.ShouldBe(new Dictionary<string, string> { ["BASIC"] = "existing", ["LOCAL"] = "local" });
    }

    [Fact]
    public void RunExpansion()
    {
        List<EnvItem> envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.expand" }];
        GetResult result = new Get("BASIC_EXPAND", envs, false, false, null).Run();

        result.Parsed["BASIC_EXPAND"].ShouldBe("basic");
    }
}