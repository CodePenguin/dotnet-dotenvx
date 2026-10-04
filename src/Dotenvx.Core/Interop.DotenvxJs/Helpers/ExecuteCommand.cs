using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/executeCommand.js. Two deliberate scope reductions
/// from the real source, neither touched by this port's own tests/parity suite: (1) no pty support
/// (upstream's `usePty` forces a pseudo-terminal for `--redact` when stdin/stdout/stderr are all
/// real TTYs, so an interactive child program keeps its normal terminal behavior while still being
/// redacted - this port always uses plain piped redirection instead, which is fine for scripted/
/// piped usage but loses some interactive-TTY fidelity for a redacted command); (2) the fuller
/// signal-forwarding set (SIGHUP/SIGQUIT/etc, grace-period forwarding, double-SIGINT force-kill)
/// isn't ported - only SIGINT via `Console.CancelKeyPress`, matching this port's pre-existing scope
/// here (unrelated to the redaction work that touched this file 2026-09-26).
/// </remarks>
public static partial class Helpers
{
    private static readonly string[] ErrorSignals = ["SIGINT", "SIGTERM"];

    private static int? GetExitCodeFromProcess(Process? process)
    {
        try
        {
            return process?.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private static async Task PumpRedactedAsync(TextReader reader, TextWriter writer,
        IReadOnlyList<string> sensitiveValues)
    {
        RedactedStreamWriter redactor = new(writer, sensitiveValues);
        char[] buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            redactor.Write(new string(buffer, 0, read));
        }

        redactor.Flush();
    }

    public static async Task<int> ExecuteCommand(IDotenvxLogger logger, string[] commandArgs,
        IDictionary<string, string> env, string[]? sensitiveValues = null)
    {
        sensitiveValues ??= [];
        logger.Debug($"executing process command [{string.Join(' ', commandArgs)}]");

        Process? child = null;
        string? signalSent = null;

        // Signal handlers - in C#, use Console.CancelKeyPress for SIGINT
        void SigintHandler(object? sender, ConsoleCancelEventArgs e)
        {
            logger.Debug("received SIGINT");
            logger.Debug("checking command process");
            logger.Debug(JsonStringify(child));

            if (child != null)
            {
                logger.Debug("sending SIGINT to command process");
                signalSent = "SIGINT";
                child.Kill(); // Send kill to the command process
            }
            else
            {
                logger.Debug("no command process to send SIGINT to");
            }
        }

        try
        {
            // Expand only the executable; child arguments and separators pass through unchanged.
            bool shellFallback = false;
            try
            {
                string? foundPath = WhichSync(commandArgs[0]);
                if (foundPath != null)
                {
                    commandArgs[0] = Path.GetFullPath(foundPath);
                    logger.Debug($"expanding process command to [{string.Join(' ', commandArgs)}]");
                }
                else
                {
                    shellFallback = true;
                }
            }
            catch
            {
                shellFallback = true;
                logger.Debug($"could not expand process command. using [{string.Join(' ', commandArgs)}]");
            }

            bool redact = sensitiveValues.Length > 0;

            child = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = commandArgs[0],
                    UseShellExecute = false,
                    RedirectStandardOutput = redact,
                    RedirectStandardError = redact,
                    RedirectStandardInput = false
                }
            };

            if (redact)
            {
                child.StartInfo.StandardOutputEncoding = new UTF8Encoding(false);
                child.StartInfo.StandardErrorEncoding = new UTF8Encoding(false);
            }

            foreach (string arg in commandArgs.Skip(1))
            {
                child.StartInfo.ArgumentList.Add(arg);
            }

            if (shellFallback)
            {
                ApplyComSpecFallback(child.StartInfo, commandArgs);
            }

            // Set environment variables
            foreach (KeyValuePair<string, string> kvp in env)
            {
                child.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            // Inherit environment variables from current process
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (!child.StartInfo.EnvironmentVariables.ContainsKey(entry.Key.ToString()!))
                {
                    child.StartInfo.EnvironmentVariables[entry.Key.ToString()!] = entry.Value?.ToString();
                }
            }

            // Add signal handlers
            Console.CancelKeyPress += SigintHandler;

            child.Start();

            Task stdoutTask = redact
                ? PumpRedactedAsync(child.StandardOutput, Console.Out, sensitiveValues)
                : Task.CompletedTask;
            Task stderrTask = redact
                ? PumpRedactedAsync(child.StandardError, Console.Error, sensitiveValues)
                : Task.CompletedTask;

            // Wait for the command process to finish
            await child.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);

            int exitCode = GetExitCodeFromProcess(child) ?? 1;

            if (exitCode == 0)
            {
                return 0;
            }

            // No error message here, matching upstream: a plain nonzero exit from the wrapped
            // command is never logged by dotenvx itself (the child's own stderr already said
            // whatever it wanted to say) - only genuine process-start/wait failures below get a
            // logged message.
            logger.Debug($"received exitCode {exitCode}");
            return exitCode;
        }
        catch (Exception error)
        {
            // Handle errors
            if (ErrorSignals.Contains(signalSent ?? error.Message))
            {
                return GetExitCodeFromProcess(child) ?? 1;
            }

            logger.Error(error is Win32Exception { NativeErrorCode: 2 /*ERROR_FILE_NOT_FOUND*/ }
                ? $"Unknown command: {commandArgs[0]}"
                : RedactOutputText(error.Message, sensitiveValues) ?? error.Message);

            // Return the error code from the command process, or 1 if unavailable
            return GetExitCodeFromProcess(child) ?? 1;
        }
        finally
        {
            Console.CancelKeyPress -= SigintHandler;
        }
    }

    /// <summary>
    /// Reproduces cross-spawn's Windows shell fallback, which is what upstream reaches through
    /// execa when `which.sync` cannot resolve the command. Node's libuv re-runs an unresolved
    /// command through <c>%COMSPEC%</c> as <c>cmd.exe /d /s /c "..."</c>, so a cmd builtin that is
    /// never on PATH still runs: <c>dotenvx run -- echo hi</c> prints <c>"hi"</c> on Node and used to
    /// fail here with <c>Unknown command: echo</c>. It is also why a genuinely missing command
    /// surfaces cmd's own <c>'x' is not recognized as an internal or external command</c> on stderr
    /// rather than any message of ours.
    ///
    /// The exact shape matters, and was derived by running the pinned Node CLI and diffing raw bytes
    /// (not by reading the JS): each ARGUMENT is wrapped in double quotes with inner quotes
    /// backslash-escaped, the command itself is left bare, and the whole line is wrapped once more so
    /// cmd's <c>/s</c> strips exactly that outer pair. So <c>run -- echo ""</c> executes <c>echo ""</c>
    /// and prints <c>""</c>, and <c>run -- echo a&amp;b</c> executes <c>echo "a&amp;b"</c> - the quotes
    /// are what stop <c>&amp;</c> being read as a command separator.
    ///
    /// <c>Arguments</c> is set instead of <c>ArgumentList</c> because .NET only quotes values it puts
    /// in <c>ArgumentList</c>; a raw <c>Arguments</c> string is passed to CreateProcess untouched.
    /// </summary>
    private static void ApplyComSpecFallback(ProcessStartInfo startInfo, string[] commandArgs)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string commandLine = string.Join(" ",
            commandArgs.Skip(1).Select(arg => "\"" + arg.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""));

        startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") is { Length: > 0 } comSpec
            ? comSpec
            : "cmd.exe";
        startInfo.ArgumentList.Clear();
        startInfo.Arguments = $"/d /s /c \"{commandArgs[0]} {commandLine}\"";
    }

    private static string? WhichSync(string command)
    {
        // Equivalent to which.sync - find executable in PATH
        if (File.Exists(command))
        {
            return command;
        }

        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        string[] paths = pathVar?.Split(Path.PathSeparator) ?? [];
        string[] extensions = isWindows && !Path.HasExtension(command) ? [".exe", ".cmd", ".bat"] : [""];
        return paths.SelectMany(_ => extensions, (path, extension) => Path.Combine(path, command + extension))
            .FirstOrDefault(File.Exists);
    }
}