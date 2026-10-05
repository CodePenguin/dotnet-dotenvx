using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/executeDynamic.js and executeExtension.js. `process.exit(code)` becomes the
/// returned exit code and `childProcess.spawnSync` is the `SpawnCommand` seam.
/// </remarks>
public class ExecuteDynamicTest : EnvironmentTestBase
{
    private readonly List<(string Command, string[] Args)> _spawned = [];

    private void Spawn(SpawnResult result)
    {
        ExecuteDynamic.SpawnCommand = (command, args, _) =>
        {
            _spawned.Add((command, args));

            return result;
        };
    }

    private string CaptureStdout(Func<int> action, out int exitCode)
    {
        var output = new StringWriter { NewLine = "\n" };
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            exitCode = action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return output.ToString();
    }

    private static bool HasValidBoxShape(string output)
    {
        string[] lines = output.TrimEnd('\n').Split('\n');
        if (lines.Length < 3)
        {
            return false;
        }

        return Regex.IsMatch(lines[0], "^ _+$") && Regex.IsMatch(lines[^1], @"^\|_+\|$") &&
               lines[1..^1].All(line => line.StartsWith('|') && line.EndsWith('|'));
    }

    /// <remarks>Refer to "executeDynamic - no command"</remarks>
    [Fact]
    public void TestExecuteDynamicNoCommand()
    {
        bool helpShown = false;

        int exit = ExecuteDynamic.Dynamic(null, [], MockLogger, () => helpShown = true);

        helpShown.ShouldBeTrue("program help is shown");
        exit.ShouldBe(1);
    }

    /// <remarks>Refer to "executeDynamic - other command missing"</remarks>
    [Fact]
    public void TestExecuteDynamicOtherCommandMissing()
    {
        Spawn(new SpawnResult(1, true));

        int exit = ExecuteDynamic.Dynamic("other", ["other"], MockLogger);

        _spawned.Single().Command.ShouldBe("dotenvx-other");
        _spawned.Single().Args.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("error: unknown command 'other'");
        exit.ShouldBe(1);
    }

    /// <remarks>Refer to "executeDynamic - pro found"</remarks>
    [Fact]
    public void TestExecuteDynamicProFound()
    {
        Spawn(new SpawnResult(0, false));

        int exit = ExecuteDynamic.Dynamic("pro", ["pro"], MockLogger);

        _spawned.Single().Command.ShouldBe("dotenvx-pro");
        exit.ShouldBe(0);
    }

    /// <remarks>Refer to "executeDynamic - armor found"</remarks>
    [Fact]
    public void TestExecuteDynamicArmorFound()
    {
        Spawn(new SpawnResult(0, false));

        int exit = ExecuteDynamic.Dynamic("armor", ["armor", "up"], MockLogger);

        _spawned.Single().Command.ShouldBe("dotenvx-armor");
        _spawned.Single().Args.ShouldBe(["up"]);
        exit.ShouldBe(0);
    }

    /// <remarks>Refer to "executeDynamic - armor command missing"</remarks>
    [Fact]
    public void TestExecuteDynamicArmorCommandMissing()
    {
        Spawn(new SpawnResult(1, true));

        string output = CaptureStdout(() => ExecuteDynamic.Dynamic("armor", ["armor"], MockLogger), out int exit);

        _spawned.Single().Command.ShouldBe("dotenvx-armor");
        output.ShouldMatch("(?i)Install one");
        output.ShouldContain("[curl -sfS https://dotenvx.sh/armor | sh]");
        output.ShouldContain("[npm i @dotenvx/dotenvx-armor --save]");
        output.ShouldMatch("(?i)Then");
        output.ShouldContain("[dotenvx armor up]");
        output.ShouldContain("(sign in when prompted)");
        HasValidBoxShape(output).ShouldBeTrue("banner box shape is valid");
        exit.ShouldBe(1);
    }

    /// <remarks>Refer to "executeExtension - no command"</remarks>
    [Fact]
    public void TestExecuteExtensionNoCommand()
    {
        bool helpShown = false;

        int exit = ExecuteDynamic.Extension(null, [], MockLogger, () => helpShown = true);

        helpShown.ShouldBeTrue();
        exit.ShouldBe(0);
    }

    /// <remarks>Refer to "executeExtension - vault command missing"</remarks>
    [Fact]
    public void TestExecuteExtensionVaultCommandMissing()
    {
        Spawn(new SpawnResult(1, true));

        int exit = ExecuteDynamic.Extension("vault", ["vault"], MockLogger);

        _spawned.ShouldNotBeEmpty();
        exit.ShouldBe(1);
        MockLogger.WarningMessages.ShouldContain(
            "[INSTALLATION_NEEDED] install dotenvx-ext-vault to use [dotenvx ext vault] commands");
        MockLogger.HelpMessages.ShouldContain("? see installation instructions [https://github.com/dotenvx/dotenvx-ext-vault]");
    }

    /// <remarks>Refer to "executeExtension - other command missing"</remarks>
    [Fact]
    public void TestExecuteExtensionOtherCommandMissing()
    {
        Spawn(new SpawnResult(1, true));

        int exit = ExecuteDynamic.Extension("other", ["other"], MockLogger);

        exit.ShouldBe(1);
        MockLogger.WarningMessages.ShouldBeEmpty();
        MockLogger.HelpMessages.ShouldBeEmpty();
        MockLogger.InfoMessages.ShouldContain("error: unknown command 'other'");
    }

    /// <remarks>Refer to "executeExtension - vault found"</remarks>
    [Fact]
    public void TestExecuteExtensionVaultFound()
    {
        Spawn(new SpawnResult(0, false));

        int exit = ExecuteDynamic.Extension("vault", ["vault"], MockLogger);

        _spawned.Single().Command.ShouldBe("dotenvx-ext-vault");
        exit.ShouldBe(0);
        MockLogger.WarningMessages.ShouldBeEmpty();
        MockLogger.HelpMessages.ShouldBeEmpty();
    }
}
