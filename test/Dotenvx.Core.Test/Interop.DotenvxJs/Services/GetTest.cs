using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/get.test.js</remarks>
public class GetTest : EnvironmentTestBase
{
    public GetTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "#run (missing key returns the entire processEnv as object)"</remarks>
    [Fact]
    public void TestRunMissingKeyReturnsTheEntireProcessEnvAsObject()
    {
        GetResult result = new Get(null, [], false, false, null).Run();

        result.Parsed.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (all object) with preset process.env"</remarks>
    [Fact]
    public void TestRunAllObjectWithPresetProcessEnv()
    {
        Environment.SetEnvironmentVariable("PRESET_ENV_EXAMPLE", "something/on/machine");

        GetResult result = new Get(null, [], false, true, []).Run();
        result.Parsed.ShouldBe(new Dictionary<string, string> { ["PRESET_ENV_EXAMPLE"] = "something/on/machine" });

        result = new Get(null, [], false, false, []).Run();
        result.Parsed.ShouldBeEmpty();
    }

    /// <remarks>Refer to "#run (missing key returns the entire processEnv as object)"</remarks>
    [Fact]
    public void TestRunMissingKeyReturnsTheEntireProcessEnvAsObjectWithEnvFiles()
    {
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get(null, envs, false, false, null).Run();

        result.Parsed.ShouldBe(new Dictionary<string, string> { ["BASIC"] = "local_basic", ["LOCAL"] = "local" });
    }

    /// <remarks>Refer to "#run (missing key returns empty string when fetching single key)"</remarks>
    [Fact]
    public void TestRunMissingKeyReturnsEmptyStringWhenFetchingSingleKey()
    {
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get("BAZ", envs, false, false, null).Run();

        result.Parsed.ToDictionary().ShouldNotContainKey("BAZ");
    }

    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRun()
    {
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env" }];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    /// <remarks>Refer to "#run (as multi-array)"</remarks>
    [Fact]
    public void TestRunAsMultiArray()
    {
        EnvItem[] envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }
        ];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    /// <remarks>Refer to "#run (as multi-array reversed (first wins))"</remarks>
    [Fact]
    public void TestRunAsMultiArrayReversedFirstWins()
    {
        EnvItem[] envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env.local" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env" }
        ];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("local_basic");
    }

    /// <remarks>Refer to "#run (as multi-array reversed with overload (second wins))"</remarks>
    [Fact]
    public void TestRunAsMultiArrayReversedWithOverloadSecondWins()
    {
        EnvItem[] envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env.local" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env" }
        ];
        GetResult result = new Get("BASIC", envs, true, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    [Fact]
    public void TestRunAsMultiArraySomeNotFound()
    {
        EnvItem[] envs =
        [
            new() { Type = EnvType.EnvFile, Value = "tests/.env.notfound" },
            new() { Type = EnvType.EnvFile, Value = "tests/.env" }
        ];
        GetResult result = new Get("BASIC", envs, true, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("basic");
    }

    /// <remarks>Refer to "#run (process.env already exists on machine)"</remarks>
    [Fact]
    public void TestRunProcessEnvAlreadyExistsOnMachine()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get("BASIC", envs, false, false, null).Run();

        result.Parsed["BASIC"].ShouldBe("existing");
    }

    [Fact]
    public void TestRunNoKeyAndProcessEnvAlreadyExistsOnMachine()
    {
        Environment.SetEnvironmentVariable("BASIC", "existing");

        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.local" }];
        GetResult result = new Get(null, envs, false, false, null).Run();

        result.Parsed.ShouldBe(new Dictionary<string, string> { ["BASIC"] = "existing", ["LOCAL"] = "local" });
    }

    /// <remarks>Refer to "#run expansion"</remarks>
    [Fact]
    public void TestRunExpansion()
    {
        EnvItem[] envs = [new() { Type = EnvType.EnvFile, Value = "tests/.env.expand" }];
        GetResult result = new Get("BASIC_EXPAND", envs, false, false, null).Run();

        result.Parsed["BASIC_EXPAND"].ShouldBe("basic");
    }
}