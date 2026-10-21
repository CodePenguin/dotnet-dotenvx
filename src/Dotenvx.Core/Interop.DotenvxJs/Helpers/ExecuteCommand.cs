using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/executeCommand.js. `--redact` pipes the child's output through the redactor,
/// wrapped in `script` (<see cref="PtyCommand" />) when stdin, stdout and stderr are all terminals. On Unix the full
/// signal-forwarding set lives in <see cref="SignalForwarder" />; Windows kills the child (and its tree) on Ctrl+C.
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
        bool shellFallback = false;
        string? signalSent = null;
        SignalForwarder? forwarder = null;

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
                child.Kill(shellFallback); // with the %COMSPEC% fallback the child is cmd.exe: take its tree with it
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

            // executeCommand.js: `usePty` when redacting with stdin, stdout and stderr all terminals - the child then runs under `script`
            bool usePty = redact && !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;
            string[] executedArgs = (usePty ? PtyCommand.Wrap(commandArgs) : null) ?? commandArgs;

            child = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executedArgs[0],
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

            foreach (string arg in executedArgs.Skip(1))
            {
                child.StartInfo.ArgumentList.Add(arg);
            }

            if (shellFallback)
            {
                ApplyComSpecFallback(child.StartInfo, commandArgs);
            }

            // `{ ...process.env, ...env }`: the machine's variables keep their position, an overriding value replaces one in place and new
            // keys come last (a child that prints its environment shows the order)
            // (StartInfo.Environment starts as a Hashtable snapshot in hash order, so it is refilled in the order of the C environ block)
            IDictionary<string, string?> childEnvironment = child.StartInfo.Environment;
            childEnvironment.Clear();
            foreach ((string key, string? value) in MachineEnvironment())
            {
                childEnvironment[key] = value;
            }

            foreach (KeyValuePair<string, string> kvp in env)
            {
                // the operating system's environment block is NUL-terminated: a value ends at its first NUL (libuv does the same),
                // where .NET would write the rest as further variables
                int nul = kvp.Value.IndexOf('\0');
                child.StartInfo.EnvironmentVariables[kvp.Key] = nul < 0 ? kvp.Value : kvp.Value[..nul];
            }

            // Add signal handlers
            if (OperatingSystem.IsWindows())
            {
                Console.CancelKeyPress += SigintHandler;
            }

            child.Start();

            Task<string?> killedBy = ChildSignal.WatchAsync(child.Id);

            if (!OperatingSystem.IsWindows())
            {
                Process started = child;
                forwarder = new SignalForwarder(logger, !Console.IsInputRedirected, _ => !started.HasExited,
                    signal => SignalForwarder.Send(started, signal), signal => signalSent = signal);
                forwarder.Register();
            }

            Task stdoutTask = redact
                ? PumpRedactedAsync(child.StandardOutput, Console.Out, sensitiveValues)
                : Task.CompletedTask;
            Task stderrTask = redact
                ? PumpRedactedAsync(child.StandardError, Console.Error, sensitiveValues)
                : Task.CompletedTask;

            // Wait for the command process to finish
            await child.WaitForExitAsync();
            await Task.WhenAll(stdoutTask, stderrTask);

            // execa leaves exitCode undefined for a child killed by a signal: dotenvx exits 1, and reports it unless the signal was SIGINT or SIGTERM
            string? killSignal = await killedBy;
            if (killSignal != null)
            {
                Events.EventScope.SignalReceived(signalSent ?? killSignal);
                if (!ErrorSignals.Contains(signalSent ?? killSignal))
                {
                    string description = ChildSignal.Describe(killSignal) is { } text ? $" ({text})" : "";
                    logger.Error(RedactOutputText($"Command was killed with {killSignal}{description}: {string.Join(' ', executedArgs)}", sensitiveValues)
                        ?? "");
                }

                return 1;
            }

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
            Events.EventScope.SignalReceived(signalSent);
            return exitCode;
        }
        catch (Exception error)
        {
            // Handle errors
            Events.EventScope.SignalReceived(signalSent);
            if (error is Win32Exception { NativeErrorCode: 2 })
            {
                Events.EventScope.Fail("COMMAND_NOT_FOUND");
            }

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
            forwarder?.Dispose();
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
    /// Security note (from code review): cmd.exe does not honor backslash escapes, so an argument containing a double
    /// quote followed by cmd metacharacters can still break out of the quoting, as in the cross-spawn behavior this
    /// copies. The arguments are the caller's own command line (same trust domain as typing it in cmd directly), but
    /// do not feed untrusted text through `dotenvx run` on Windows when the command is a cmd builtin.
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
        // cross-spawn normalizes the command like path.normalize (empty becomes ".", "./x" becomes "x"), which decides what cmd.exe reports
        startInfo.Arguments = $"/d /s /c \"{Helpers.NodePathJoin(commandArgs[0], "")} {commandLine}\"";
    }

    [DllImport("libc")]
    private static extern IntPtr _NSGetEnviron();

    /// <summary>The variables in the order of the C environ block, which is the order of Node's process.env; variables .NET holds
    /// that the block lacks come last.</summary>
    private static List<KeyValuePair<string, string?>> MachineEnvironment()
    {
        IDictionary current = Environment.GetEnvironmentVariables();
        List<KeyValuePair<string, string?>> ordered = [];
        HashSet<string> seen = [];
        foreach (string key in EnvironBlockKeys())
        {
            if (current.Contains(key) && seen.Add(key))
            {
                ordered.Add(new(key, current[key]?.ToString()));
            }
        }

        foreach (DictionaryEntry entry in current)
        {
            if (seen.Add(entry.Key.ToString()!))
            {
                ordered.Add(new(entry.Key.ToString()!, entry.Value?.ToString()));
            }
        }

        return ordered;
    }

    private static IEnumerable<string> EnvironBlockKeys()
    {
        List<string> entries = [];
        try
        {
            if (OperatingSystem.IsLinux())
            {
                entries.AddRange(Encoding.UTF8.GetString(File.ReadAllBytes("/proc/self/environ")).Split('\0'));
            }
            else if (OperatingSystem.IsMacOS())
            {
                IntPtr environ = Marshal.ReadIntPtr(_NSGetEnviron());
                for (int i = 0; environ != IntPtr.Zero && Marshal.ReadIntPtr(environ, i * IntPtr.Size) is var entry && entry != IntPtr.Zero; i++)
                {
                    entries.Add(Marshal.PtrToStringUTF8(entry) ?? "");
                }
            }
        }
        catch (Exception)
        {
            // no order to follow: the Hashtable order stands
        }

        return entries.Where(entry => entry.IndexOf('=') > 0).Select(entry => entry[..entry.IndexOf('=')]);
    }

    /// <summary>
    /// `which.sync(command)` (which 6.x): a command with a slash is checked as given; otherwise each PATH directory is searched
    /// (Windows also searches the current directory first, trying every PATHEXT extension in upper and lower case).
    /// </summary>
    private static string? WhichSync(string command)
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        char delimiter = Path.PathSeparator;
        bool hasSlash = command.Contains('/') || (isWindows && command.Contains('\\'));
        string[] pathEnv = hasSlash
            ? [""]
            :
            [
                .. isWindows ? [Directory.GetCurrentDirectory()] : Array.Empty<string>(),
                .. (Environment.GetEnvironmentVariable("PATH") ?? "").Split(delimiter)
            ];

        string pathExtExe = "";
        List<string> pathExt = [""];
        if (isWindows)
        {
            string configured = Environment.GetEnvironmentVariable("PATHEXT") ?? "";
            pathExtExe = configured.Length > 0 ? configured : string.Join(delimiter, ".EXE", ".CMD", ".BAT", ".COM");
            pathExt = [.. pathExtExe.Split(delimiter).SelectMany(item => new[] { item, item.ToLowerInvariant() })];
            if (command.Contains('.') && pathExt[0] != "")
            {
                pathExt.Insert(0, "");
            }
        }

        foreach (string envPart in pathEnv)
        {
            string pathPart = envPart.Length >= 2 && envPart[0] == '"' && envPart[^1] == '"' ? envPart[1..^1] : envPart;
            string joined = pathPart.Length == 0 ? command : Path.Combine(pathPart, command);
            if (pathPart.Length == 0 && (command.StartsWith("./", StringComparison.Ordinal) || command.StartsWith(".\\", StringComparison.Ordinal)))
            {
                joined = command;
            }

            foreach (string extension in pathExt)
            {
                string candidate = joined + extension;
                if (IsExecutable(candidate, isWindows, pathExtExe.Split(delimiter)))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>isexe: a file that is runnable (a PATHEXT extension on Windows, an execute bit elsewhere).</summary>
    private static bool IsExecutable(string path, bool isWindows, string[] pathExt)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            if (isWindows)
            {
                if (pathExt.Length == 0 || pathExt.All(item => item.Length == 0))
                {
                    return true;
                }

                return pathExt.Any(item => item.Length > 0 && path.EndsWith(item, StringComparison.OrdinalIgnoreCase));
            }

            #pragma warning disable CA1416 // reached only when not Windows: isWindows is a parameter so tests can exercise both branches
            const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

            return (File.GetUnixFileMode(path) & anyExecute) != 0;
            #pragma warning restore CA1416
        }
        catch (Exception)
        {
            return false;
        }
    }
}
