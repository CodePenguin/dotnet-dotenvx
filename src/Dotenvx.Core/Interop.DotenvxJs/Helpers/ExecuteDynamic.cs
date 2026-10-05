using System.Diagnostics;

using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/executeDynamic.js and executeExtension.js - an unknown command is handed to
/// an external `dotenvx-&lt;command&gt;` (or `dotenvx-ext-&lt;command&gt;`) program found on PATH, with stdio inherited. The
/// child's PATH is `&lt;cwd&gt;/node_modules/.bin:&lt;PATH&gt;` joined with a literal colon even on Windows, exactly as upstream
/// builds it (so the first original PATH entry is merged into the first one there; verified against the Node CLI).
/// </remarks>
/// <summary>Node's `spawnSync` result: `status` (null when killed or never started) and whether `error` was set.</summary>
public record SpawnResult(int? Status, bool Failed);

public static class ExecuteDynamic
{
    private static readonly Func<string, string[], string?, SpawnResult> DefaultSpawn = Spawn;

    /// <summary>(command, args, PATH) => the `spawnSync` result.</summary>
    public static Func<string, string[], string?, SpawnResult> SpawnCommand { get; internal set; } = DefaultSpawn;

    internal static void ResetToDefaults()
    {
        SpawnCommand = DefaultSpawn;
    }

    /// <summary>
    /// `executeDynamic(program, command, rawArgs)`: forwards to `dotenvx-&lt;command&gt;`. Returns the process exit code
    /// (`process.exit(result.status)` when it is non-zero; a missing program exits 0 after printing the not-found text).
    /// </summary>
    public static int Dynamic(string? command, string[] rawArgs, IDotenvxLogger logger, Action? outputHelp = null)
    {
        if (string.IsNullOrEmpty(command))
        {
            outputHelp?.Invoke();

            return 1;
        }

        return Run("dotenvx-", command, rawArgs, logger, banner: command == "armor");
    }

    /// <summary>`executeExtension(ext, command, rawArgs)`: forwards to `dotenvx-ext-&lt;command&gt;`.</summary>
    public static int Extension(string? command, string[] rawArgs, IDotenvxLogger logger, Action? outputHelp = null)
    {
        if (string.IsNullOrEmpty(command))
        {
            outputHelp?.Invoke();

            return 0;
        }

        return Run("dotenvx-ext-", command, rawArgs, logger, banner: false);
    }

    private static int Run(string prefix, string command, string[] rawArgs, IDotenvxLogger logger, bool banner)
    {
        // construct the full command line manually including flags
        int commandIndex = Array.IndexOf(rawArgs, command);
        string[] forwardedArgs = rawArgs[(commandIndex + 1)..];

        logger.Debug($"command: {command}");
        logger.Debug($"args: {JsonStringify(forwardedArgs)}");

        string binPath = Path.Combine(Directory.GetCurrentDirectory(), "node_modules", ".bin");
        string newPath = $"{binPath}:{Environment.GetEnvironmentVariable("PATH")}";

        SpawnResult result = SpawnCommand(prefix + command, forwardedArgs, newPath);

        if (result.Failed)
        {
            if (banner)
            {
                Console.Out.WriteLine(ArmorBanner.Render());
            }
            else if (prefix == "dotenvx-ext-" && command == "vault")
            {
                logger.Warn($"[INSTALLATION_NEEDED] install dotenvx-ext-{command} to use [dotenvx ext {command}] commands");
                logger.Help("? see installation instructions [https://github.com/dotenvx/dotenvx-ext-vault]");
            }
            else
            {
                logger.Info($"error: unknown command '{command}'");
            }

        }

        // `if (result.status !== 0) process.exit(result.status)`: a null status exits 0
        return result.Status ?? 0;
    }

    private static string JsonStringify(string[] values)
    {
        return System.Text.Json.JsonSerializer.Serialize(values, new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static SpawnResult Spawn(string command, string[] args, string? path)
    {
        string? resolved = SecretCli.ResolveExecutable(command, path);
        if (resolved is null)
        {
            return new SpawnResult(null, true);
        }

        var psi = new ProcessStartInfo { FileName = resolved, UseShellExecute = false };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.EnvironmentVariables["PATH"] = path ?? "";

        try
        {
            using Process process = Process.Start(psi)!;
            process.WaitForExit();

            return new SpawnResult(process.ExitCode, false);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new SpawnResult(null, true);
        }
    }
}
