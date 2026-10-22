namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/ptyCommand.js. With `--redact` the child's output is piped through the
/// redactor; when stdin, stdout and stderr are all terminals the child is wrapped in `script` so it still sees a pty.
/// </remarks>
internal static class PtyCommand
{
    private static readonly Func<string, string?> DefaultWhich = name => SecretCli.ResolveExecutable(name);

    /// <summary>`which.sync('script')`; null when it is not on PATH. Swappable for tests.</summary>
    public static Func<string, string?> Which { get; internal set; } = DefaultWhich;

    internal static void ResetToDefaults()
    {
        Which = DefaultWhich;
    }

    private static string ShellQuote(string value)
    {
        // `'${value.replace(/'/g, '\'"\'"\'')}'`
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    /// <summary>The command wrapped for a pseudo-terminal, or null when unavailable (unsupported platform or no `script`).</summary>
    public static string[]? Wrap(string[] commandArgs, string? platform = null)
    {
        platform ??= OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : "win32";
        if (platform is not ("darwin" or "linux"))
        {
            return null;
        }

        string? found = Which("script");
        if (found is null)
        {
            return null;
        }

        string script = Path.GetFullPath(found);

        if (platform == "darwin")
        {
            return [script, "-q", "/dev/null", .. commandArgs];
        }

        // util-linux script only accepts the child command as a shell string.
        return [script, "-qefc", string.Join(" ", commandArgs.Select(ShellQuote)), "/dev/null"];
    }
}
