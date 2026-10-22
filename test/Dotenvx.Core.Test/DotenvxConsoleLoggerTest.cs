using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test;

/// <remarks>Reference source: dotenvx/src/shared/logger.js, and main.js using `logger` whenever no other logger is given.</remarks>
public class DotenvxConsoleLoggerTest : EnvironmentTestBase
{
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public DotenvxConsoleLoggerTest()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
        Dotenvx.Logger.SetLevel(DotenvxLogLevel.Info);
    }

    public override void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Dotenvx.Logger.SetLevel(DotenvxLogLevel.Info);
        base.Dispose();
    }

    /// <remarks>Refer to logger.js: `info` goes to stdout, the other levels to stderr</remarks>
    [Fact]
    public void TestInfoGoesToStdoutAndTheRestToStderr()
    {
        var logger = new DotenvxConsoleLogger();

        logger.Info("an info");
        logger.Success("a success");
        logger.Warn("a warning");
        logger.Error("an error");

        _out.ToString().ShouldContain("an info");
        _out.ToString().ShouldNotContain("a success");
        _error.ToString().ShouldContain("a success");
        _error.ToString().ShouldContain("⚠ a warning");
        _error.ToString().ShouldContain("☠ an error");
    }

    /// <remarks>Refer to logger.js: a message above the current level is not written</remarks>
    [Fact]
    public void TestVerboseAndDebugWaitForTheirLevel()
    {
        var logger = new DotenvxConsoleLogger();

        logger.Verbose("verbose one");
        logger.Debug("debug one");
        _error.ToString().ShouldBeEmpty();

        logger.SetLevel(DotenvxLogLevel.Verbose);
        logger.Verbose("verbose two");
        logger.Debug("debug two");
        _error.ToString().ShouldContain("┆ verbose two");
        _error.ToString().ShouldNotContain("debug two");
    }

    /// <remarks>Refer to main.js `setLogLevel` and `logger.level`</remarks>
    [Fact]
    public void TestSetLogLevelFollowsTheOptions()
    {
        Dotenvx.SetLogLevel(new ConfigOptions());
        Dotenvx.Logger.Level.ShouldBe(DotenvxLogLevel.Info);

        Dotenvx.SetLogLevel(new ConfigOptions { Verbose = true });
        Dotenvx.Logger.Level.ShouldBe(DotenvxLogLevel.Verbose);

        Dotenvx.SetLogLevel(new ConfigOptions { Quiet = true });
        Dotenvx.Logger.Level.ShouldBe(DotenvxLogLevel.Error);
    }

    /// <remarks>Refer to main.js: `config` logs through `logger` when no other logger is given</remarks>
    [Fact]
    public void TestConfigWithoutALoggerUsesTheDefaultLogger()
    {
        Main.Config(new ConfigOptions { Strict = true, ProcessEnv = new Dictionary<string, string>(), Envs = [new EnvItem { Type = EnvType.Env, Value = "A=1" }] });

        _error.ToString().ShouldContain("injected env");
    }

    /// <remarks>Refer to main.js: `quiet` lowers the level to `error`</remarks>
    [Fact]
    public void TestQuietSilencesTheDefaultLogger()
    {
        Main.Config(new ConfigOptions
        {
            Quiet = true,
            ProcessEnv = new Dictionary<string, string>(),
            Envs = [new EnvItem { Type = EnvType.Env, Value = "A=1" }]
        });

        _error.ToString().ShouldBeEmpty();
    }
}
