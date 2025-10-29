using System.Collections;
using System.ComponentModel;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

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
    
    public static async Task<int> ExecuteCommand(ILogger logger, string[] commandArgs, IDictionary<string, string> env)
    {
        logger.LogDebug("executing process command [{Command}]", string.Join(' ', commandArgs));

        Process? child = null;
        string? signalSent = null;

        // Signal handlers - in C#, use Console.CancelKeyPress for SIGINT
        void SigintHandler(object? sender, ConsoleCancelEventArgs e)
        {
            logger.LogDebug("received SIGINT");
            logger.LogDebug("checking command process");
            logger.LogDebug("{Child}", child);

            if (child != null)
            {
                logger.LogDebug("sending SIGINT to command process");
                signalSent = "SIGINT";
                child.Kill(); // Send kill to the command process
            }
            else
            {
                logger.LogDebug("no command process to send SIGINT to");
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
                    logger.LogDebug("expanding process command to [{Command}]", string.Join(' ', commandArgs));
                }
            }
            catch
            {
                logger.LogDebug("could not expand process command. using [{Command}]", string.Join(' ', commandArgs));
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
                            logger.LogDebug("expanding process command to [{Command}]", string.Join(' ', commandArgs));
                        }
                    }
                    catch
                    {
                        logger.LogDebug("could not expand process command. using [{Command}]",
                            string.Join(' ', commandArgs));
                    }

                    expandNext = false;
                }
            }

            child = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = commandArgs[0],
                    Arguments = string.Join(' ', commandArgs.Skip(1)),
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                }
            };

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

            logger.LogDebug("received exitCode {ExitCode}", exitCode);
            throw new Exception($"Command exited with exit code {exitCode}");
        }
        catch (Exception error)
        {
            // Handle errors
            if (ErrorSignals.Contains(signalSent ?? error.Message))
            {
                return GetExitCodeFromProcess(child) ?? 1;
            }

            if (error is Win32Exception { NativeErrorCode: 2 /*ERROR_FILE_NOT_FOUND*/ })
            {
                logger.LogError("Unknown command: {Command}", commandArgs[0]);
            }
            else
            {
                logger.LogError("{ErrorMessage}", error.Message);
            }

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