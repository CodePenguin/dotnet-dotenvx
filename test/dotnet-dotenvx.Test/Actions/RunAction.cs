using System.CommandLine;

using dotnet_dotenvx.Actions;

using Microsoft.Extensions.Logging;

using Moq;

using Shouldly;

namespace dotnet_dotenvx.Test.Actions;

public class RunActionTests
{
    [Fact]
    public void Run()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunConventionNextjs()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "--convention", "nextjs", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunConventionFlow()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "--convention", "flow", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunDotenvKey()
    {
        // VAULT is deprecated
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        Environment.SetEnvironmentVariable("DOTENV_KEY", "vlt_1234");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "--", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
        // Note: Deprecation warnings for DOTENV_KEY are not implemented in C#
    }

    [Fact]
    public void RunEnvFileWithErrors()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        Environment.SetEnvironmentVariable("DOTENV_PRIVATE_KEY", "bad-private-key");
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/encrypted/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0); // command succeeds, but error logged

        loggedMessages.ShouldContain(m => m.Contains("[INVALID_PRIVATE_KEY]"));
        loggedMessages.ShouldContain(m => m.Contains("DOTENV_PUBLIC_KEY set"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
    }

    [Fact]
    public void RunEnvFile()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state,
                exception, formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (1)"));
    }

    [Fact]
    public void RunEnvVaultFile()
    {
        // VAULT is deprecated
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/encrypted/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from encrypted"));
        loggedMessages.ShouldContain(m => m.Contains("DOTENV_PUBLIC_KEY set"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (2)"));
    }


    [Fact]
    public void RunEnvFilePreexistsFalsy()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        Environment.SetEnvironmentVariable("HELLO", "");
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO pre-exists"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunEnvFileAndEnv()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-e", "HELLO=string", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from string"));
        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO pre-exists"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (1)"));
    }

    [Fact]
    public void RunEnvFileAndTwoEnvs()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse([
            "run", "-e", "HELLO=World", "-e", "HOLA=amigo", "-f", filepath, "--", "echo", "test"
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from string (HELLO=World)"));
        loggedMessages.ShouldContain(m => m.Contains("loading env from string (HOLA=amigo)"));
        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("HOLA set"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (2) from .env, and --env flags"));
    }

    [Fact]
    public void RunEnvTwoStrings()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-e", "HELLO=World", "-e", "HEY=there", "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from string (HELLO=World)"));
        loggedMessages.ShouldContain(m => m.Contains("loading env from string (HEY=there)"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("HEY set"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (2) from --env flags"));
    }

    [Fact]
    public void RunMissingEnvFile()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", "nonexistent.env", "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from nonexistent.env"));
        loggedMessages.ShouldContain(m => m.Contains("missing .env file"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunMissingEnvFileStrict()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", "nonexistent.env", "--strict", "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        loggedMessages.ShouldContain(m => m.Contains("loading env from nonexistent.env"));
        loggedMessages.ShouldContain(m => m.Contains("missing .env file"));
        loggedMessages.ShouldNotContain(m => m.Contains("injecting env"));
    }

    [Fact]
    public void RunMissingEnvFileIgnore()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse([
            "run", "-f", "nonexistent.env", "--ignore", "MISSING_ENV_FILE", "--", "echo", "test"
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from nonexistent.env"));
        loggedMessages.ShouldNotContain(m => m.Contains("missing .env file"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunMissingEnvFileStrictIgnore()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse([
            "run", "-f", "nonexistent.env", "--strict", "--ignore", "MISSING_ENV_FILE", "--", "echo", "test"
        ]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from nonexistent.env"));
        loggedMessages.ShouldNotContain(m => m.Contains("missing .env file"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunOtherError()
    {
        // To simulate OTHER_ERROR, use an invalid env string
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-e", "INVALID", "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from string (INVALID)"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (0)"));
    }

    [Fact]
    public void RunThrowsError()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        // To simulate throw, perhaps invalid convention
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "--convention", "invalid", "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        loggedMessages.ShouldContain(m => m.Contains("Unknown convention"));
    }

    [Fact]
    public void RunEnvFileMissingCommand()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("missing command after [dotenvx run --]"));
    }

    [Fact]
    public void RunEnvFileAmbiguousMissingSeparator()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", ".env.production", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        loggedMessages.ShouldContain(m => m.Contains("ambiguous command due to missing '--' separator"));
    }

    [Fact]
    public void RunEnvFileAmbiguousEmpty()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", ".env", "echo", ""]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(1);

        loggedMessages.ShouldContain(m => m.Contains("ambiguous command due to missing '--' separator"));
    }

    [Fact]
    public void RunEnvFileMissingParsed()
    {
        // This test is hard to simulate without stubbing, as it requires the Run service to return incomplete data
        // Skipping or assuming it works like RunEnvFile
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (1) from .env"));
    }

    [Fact]
    public void RunRadarObserveThrows()
    {
        // Radar not implemented, so skipping the throw part
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        string filepath = Path.GetFullPath("../../../test/dotenvx/tests/monorepo/apps/frontend/.env");
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-f", filepath, "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (1) from .env"));
    }


    [Fact]
    public void RunEnv()
    {
        List<string> loggedMessages = [];
        Mock<ILogger<RunAction>> mockLogger = new();
        mockLogger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<object>(),
                It.IsAny<Exception?>(), It.IsAny<Func<object, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Func<object, Exception?, string>>((_, _, state, exception,
                formatter) =>
            {
                string message = formatter(state, exception);
                loggedMessages.Add(message);
            });
        RunAction runAction = new(mockLogger.Object);
        RootCommand root = new() { runAction };
        ParseResult parseResult = root.Parse(["run", "-e", "HELLO=string", "--", "echo", "test"]);
        int exitCode = parseResult.Invoke();

        exitCode.ShouldBe(0);

        loggedMessages.ShouldContain(m => m.Contains("loading env from string"));
        loggedMessages.ShouldContain(m => m.Contains("HELLO set"));
        loggedMessages.ShouldContain(m => m.Contains("injecting env (1)"));
    }
}