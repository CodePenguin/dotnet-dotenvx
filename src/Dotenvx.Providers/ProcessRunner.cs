using System.Diagnostics;
using System.Text;

namespace Dotenvx.Providers;

/// <summary>What <see cref="ProcessRunner.Run" /> runs: a program, its arguments, optional stdin, a timeout and environment.</summary>
internal sealed record ProcessRequest(string File, IReadOnlyList<string> Args, string? Input, int TimeoutMs,
    IReadOnlyDictionary<string, string>? Env, bool CaptureStderr);

/// <summary>
/// How a program ended (`execFileSync`): <see cref="NotFound" /> is the spawn failure ENOENT; a non-zero exit, a signal or a
/// timeout have no code (Node raises for all of them).
/// </summary>
internal sealed record ProcessOutcome(int? ExitCode, string Stdout, string Stderr, bool NotFound = false,
    bool TimedOut = false)
{
    public bool Succeeded => !NotFound && !TimedOut && ExitCode == 0;
}

/// <summary>
/// Runs the secret-store and password-manager programs the way @dotenvx/providers does (`execFileSync` with a timeout and
/// SIGKILL). Swappable so unit tests never start a real program or touch a real store.
/// </summary>
internal static class ProcessRunner
{
    private static readonly Func<ProcessRequest, ProcessOutcome> DefaultRun = Execute;

    public static Func<ProcessRequest, ProcessOutcome> Run { get; set; } = DefaultRun;

    internal static void ResetToDefaults()
    {
        Run = DefaultRun;
    }

    /// <summary>The real runner: spawns the program (found the way Node's `execFile` finds it) and waits for it.</summary>
    public static ProcessOutcome Execute(ProcessRequest request)
    {
        string? resolved = ResolveExecutable(request.File);
        if (resolved is null)
        {
            return new ProcessOutcome(null, "", "", NotFound: true);
        }

        ProcessStartInfo info = new(resolved)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (string arg in request.Args)
        {
            info.ArgumentList.Add(arg);
        }

        if (request.Env is not null)
        {
            info.Environment.Clear();
            foreach ((string key, string value) in request.Env)
            {
                info.Environment[key] = value;
            }
        }

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new InvalidOperationException("could not start process");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new ProcessOutcome(null, "", "", NotFound: true);
        }

        using (process)
        {
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try
            {
                if (request.Input is not null)
                {
                    process.StandardInput.Write(request.Input);
                }

                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // the program may have exited without reading its input
            }

            if (!process.WaitForExit(request.TimeoutMs))
            {
                try
                {
                    process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                }

                return new ProcessOutcome(null, "", "", TimedOut: true);
            }

            process.WaitForExit();

            return new ProcessOutcome(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
    }

    /// <summary>
    /// Node's `execFile('name')` lookup (libuv): the current directory first, then each PATH entry in order; on Windows
    /// only `.com` and `.exe` files count (never `.cmd`/`.bat`/`.ps1`), and the System32 directory is not searched
    /// before PATH the way `CreateProcess` would. Null is `ENOENT`.
    /// </summary>
    public static string? ResolveExecutable(string command, string? pathVariable = null)
    {
        bool hasDirectory = command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar);
        IEnumerable<string> directories = hasDirectory
            ? [""]
            // libuv searches the current directory first only on Windows; on Unix a file in the cwd must never run
            : [.. OperatingSystem.IsWindows() ? new[] { Environment.CurrentDirectory } : [], .. (pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)];

        foreach (string directory in directories)
        {
            foreach (string name in CandidateNames(command))
            {
                string candidate = directory == "" ? name : Path.Combine(directory.Trim('"'), name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateNames(string command)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [command];
        }

        string extension = Path.GetExtension(command).ToLowerInvariant();

        return extension is ".exe" or ".com" ? [command] : [command + ".com", command + ".exe"];
    }
}
