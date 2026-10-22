using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>Outcome of running an external secret CLI (`op`, `bw`). <see cref="NotFound" /> is Node's `ENOENT`.</summary>
internal record SecretCliResult(int ExitCode, string Stdout, bool NotFound = false)
{
    public string Stderr { get; init; } = "";
}

/// <remarks>
/// Shared process seam for resolveOnePassword.js / resolveBitwardenPassword.js (`execFile` / `execFileSync` with
/// `encoding: 'utf8', windowsHide: true`, stdin closed). Swappable so unit tests never spawn a real `op`/`bw`.
/// </remarks>
internal static class SecretCli
{
    private static readonly Func<string, string[], IReadOnlyDictionary<string, string>?, SecretCliResult> DefaultExec =
        (command, args, env) => ExecProcess(command, args, env);

    private static readonly Func<bool> DefaultIsInteractive = () =>
        !Console.IsInputRedirected && !Console.IsErrorRedirected;

    public static Func<string, string[], IReadOnlyDictionary<string, string>?, SecretCliResult> Exec
    {
        get;
        internal set;
    } = DefaultExec;

    /// <summary>resolveBitwardenPassword.js: `process.stdin.isTTY && process.stderr.isTTY`.</summary>
    public static Func<bool> IsInteractive { get; internal set; } = DefaultIsInteractive;

    /// <summary>Like <see cref="Exec" /> with text written to the child's stdin (native stores: `secret-tool store`).</summary>
    public static Func<string, string[], string?, SecretCliResult> ExecWithInput { get; internal set; } =
        (command, args, input) => ExecProcess(command, args, null, input);

    /// <summary>Like <see cref="ExecWithInput" /> with environment additions too (the Bitwarden session on a piped request).</summary>
    public static Func<string, string[], IReadOnlyDictionary<string, string>?, string?, SecretCliResult> ExecFull
    {
        get;
        internal set;
    } = (command, args, env, input) => ExecProcess(command, args, env, input);

    /// <summary>The one entry point the custodians use: picks the seam that matches what the request needs.</summary>
    public static SecretCliResult Run(string command, string[] args, IReadOnlyDictionary<string, string>? env = null,
        string? input = null)
    {
        if (input is null)
        {
            return Exec(command, args, env);
        }

        return env is null ? ExecWithInput(command, args, input) : ExecFull(command, args, env, input);
    }

    internal static void ResetToDefaults()
    {
        Exec = DefaultExec;
        ExecWithInput = (command, args, input) => ExecProcess(command, args, null, input);
        ExecFull = (command, args, env, input) => ExecProcess(command, args, env, input);
        IsInteractive = DefaultIsInteractive;
    }

    /// <summary>Node's `execFile('name')` lookup (see <see cref="Providers.ProcessRunner.ResolveExecutable" />).</summary>
    internal static string? ResolveExecutable(string command, string? pathVariable = null)
    {
        return Providers.ProcessRunner.ResolveExecutable(command, pathVariable);
    }

    private static SecretCliResult ExecProcess(
        string command, string[] args, IReadOnlyDictionary<string, string>? env, string? input = null)
    {
        Dictionary<string, string>? merged = null;
        if (env != null)
        {
            merged = Helpers.CurrentProcessEnv().Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value!);
            foreach ((string key, string value) in env)
            {
                merged[key] = value;
            }
        }

        Providers.ProcessOutcome outcome = Providers.ProcessRunner.Execute(
            new Providers.ProcessRequest(command, args, input, 120000, merged, true));
        if (outcome.NotFound)
        {
            return new SecretCliResult(-1, "", true);
        }

        return new SecretCliResult(outcome.ExitCode ?? -1, outcome.Stdout) { Stderr = outcome.Stderr };
    }
}
