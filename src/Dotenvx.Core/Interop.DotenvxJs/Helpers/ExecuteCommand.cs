using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

using Dotenvx.Core.Interop.DotenvxJs.Shared;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

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

    public static async Task<int> ExecuteCommand(IDotenvxLogger logger, string[] commandArgs,
        IDictionary<string, string> env)
    {
        logger.Debug($"executing process command [{string.Join(' ', commandArgs)}]");

        Process? child = null;
        string? signalSent = null;

        // Signal handlers - in C#, use Console.CancelKeyPress for SIGINT
        void SigintHandler(object? sender, ConsoleCancelEventArgs e)
        {
            logger.Debug("received SIGINT");
            logger.Debug("checking command process");
            logger.Debug(JsonSerializer.Serialize(child));

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
            // Ensure the first command is expanded
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

            // Expand any other commands that follow a --
            bool expandNext = false;
            for (int i = 0; i < commandArgs.Length; i++)
            {
                if (commandArgs[i] == "--")
                {
                    expandNext = true;
                }
                else if (expandNext)
                {
                    try
                    {
                        string? foundPath = WhichSync(commandArgs[i]);
                        if (foundPath != null)
                        {
                            commandArgs[i] = Path.GetFullPath(foundPath);
                            logger.Debug($"expanding process command to [{string.Join(' ', commandArgs)}]");
                        }
                    }
                    catch
                    {
                        logger.Debug($"could not expand process command. using [{string.Join(' ', commandArgs)}]");
                    }

                    expandNext = false;
                }
            }

            child = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = commandArgs[0],
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                }
            };

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

            // Wait for the command process to finish
            await child.WaitForExitAsync();

            int exitCode = GetExitCodeFromProcess(child) ?? 1;

            if (exitCode == 0)
            {
                return 0;
            }

            logger.Debug($"received exitCode {exitCode}");
            throw new Exception($"Command exited with exit code {exitCode}");
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
                : error.Message);

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
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        string[]? paths = pathVar?.Split(Path.PathSeparator);
        return paths?.Select(p => Path.Combine(p, command))
            .FirstOrDefault(File.Exists);
    }
}