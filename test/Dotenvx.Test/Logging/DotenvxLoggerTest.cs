using Dotenvx;
using Dotenvx.Core.Test;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Logging;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace dotnet_dotenvx.Test.Logging;

/// <remarks>
/// Reference source: dotenvx/src/shared/logger.js. No prior test coverage existed for this class
/// at all (found 2026-09-25 while chasing a `scan` parity-test failure back to its root cause) -
/// every scenario here is a regression test for that fix: error/warn/verbose/debug's missing
/// symbol prefixes, info's missing color, success's wrong color name, and the fact that none of
/// these degrade to plain text when output isn't a terminal (breaking piped/redirected output).
/// </remarks>
public class DotenvxLoggerTest : EnvironmentTestBase
{
    private static (DotenvxLogger Logger, FakeLogger Fake) CreateLogger(DotenvxLogLevel level = DotenvxLogLevel.Debug)
    {
        FakeLogger fake = new();
        DotenvxLogger logger = new(fake, new DotenvxLoggerOptions { LogLevel = level });
        return (logger, fake);
    }

    [Fact]
    public void TestErrorAddsSkullPrefixAndIsPlainWhenNotATty()
    {
        ConsoleTty.IsStdErrTty = () => false;
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Error("boom");

        fake.Messages.ShouldContain("☠ boom");
    }

    [Fact]
    public void TestErrorIsBoldAndRedWhenATty()
    {
        ConsoleTty.IsStdErrTty = () => true;
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Error("boom");

        fake.Messages.ShouldContain("\x1b[1m\x1b[38;5;124m☠ boom\x1b[39m\x1b[22m");
    }

    [Fact]
    public void TestWarnAddsWarningPrefix()
    {
        ConsoleTty.IsStdErrTty = () => false;
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Warn("careful");

        fake.Messages.ShouldContain("⚠ careful");
    }

    [Fact]
    public void TestSuccessUsesAmberNotGreenAndAddsNoPrefixOfItsOwn()
    {
        ConsoleTty.IsStdErrTty = () => true;
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Success("⟐ injected env (1)");

        fake.Messages.ShouldContain("\x1b[38;5;136m⟐ injected env (1)\x1b[39m");
    }

    [Fact]
    public void TestInfoIsPlainWhenNotATty()
    {
        ConsoleTty.IsStdOutTty = () => false;
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Info("○ no change (.env)");

        fake.Messages.ShouldContain("○ no change (.env)");
    }

    [Fact]
    public void TestInfoIsGrayWhenATty()
    {
        ConsoleTty.IsStdOutTty = () => true;
        Environment.SetEnvironmentVariable("TERM", "xterm-256color");
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Info("○ no change (.env)");

        fake.Messages.ShouldContain("\x1b[38;5;244m○ no change (.env)\x1b[39m");
    }

    [Fact]
    public void TestVerboseAddsPipePrefixOnlyAtOrAboveVerboseLevel()
    {
        ConsoleTty.IsStdErrTty = () => false;
        (DotenvxLogger quiet, FakeLogger quietFake) = CreateLogger(DotenvxLogLevel.Info);
        quiet.Verbose("HELLO set");
        quietFake.Messages.ShouldBeEmpty();

        (DotenvxLogger verbose, FakeLogger verboseFake) = CreateLogger(DotenvxLogLevel.Verbose);
        verbose.Verbose("HELLO set");
        verboseFake.Messages.ShouldContain("┆ HELLO set");
    }

    [Fact]
    public void TestDebugAddsPipePrefixOnlyAtDebugLevel()
    {
        ConsoleTty.IsStdErrTty = () => false;
        (DotenvxLogger verbose, FakeLogger verboseFake) = CreateLogger(DotenvxLogLevel.Verbose);
        verbose.Debug("options: {}");
        verboseFake.Messages.ShouldBeEmpty();

        (DotenvxLogger debug, FakeLogger debugFake) = CreateLogger(DotenvxLogLevel.Debug);
        debug.Debug("options: {}");
        debugFake.Messages.ShouldContain("┆ options: {}");
    }

    [Fact]
    public void TestHelpIsPlainWhenNotATty()
    {
        ConsoleTty.IsStdErrTty = () => false;
        (DotenvxLogger logger, FakeLogger fake) = CreateLogger();

        logger.Help("fix: install gitleaks:      [brew install gitleaks]");

        fake.Messages.ShouldContain("fix: install gitleaks:      [brew install gitleaks]");
    }

    private sealed class FakeLogger : ILogger<DotenvxLogger>
    {
        private readonly List<string> _messages = [];
        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }
}
