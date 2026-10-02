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
            try
            {
                string? foundPath = WhichSync(commandArgs[0]);
                if (foundPath != null)
                {
                    commandArgs[0] = Path.GetFullPath(foundPath);
                    logger.Debug($"expanding process command to [{string.Join(' ', commandArgs)}]");
                }
            }
            catch
            {
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