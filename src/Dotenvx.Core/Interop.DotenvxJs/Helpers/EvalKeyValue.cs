using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/evalKeyValue.js</remarks>
public static partial class Helpers
{
    public static string EvalKeyValue(string key, string value, IDictionary<string, string> processEnv,
        Dictionary<string, string> runningParsed)
    {
        // Match everything between the outermost $() using a regex with non-capturing groups
        MatchCollection matches = EvalRegex().Matches(value);

        return matches.Aggregate(value, (newValue, match) =>
        {
            // Extract command by removing $() wrapper
            string command = match.Value[2..^1];
            string result;

            try
            {
                // Combine environment variables (including runningParsed)
                Dictionary<string, string> envVars = new(processEnv);
                foreach (KeyValuePair<string, string> kvp in runningParsed)
                {
                    envVars[kvp.Key] = kvp.Value;
                }

                result = ExecSync(command, envVars);
            }
            catch (Exception ex) when (ex is not DotenvxException)
            {
                throw new CommandSubstitutionFailed(key, command, ex.Message.Trim());
            }

            // Chomp the result and replace match with result
            result = result.TrimEnd('\n', '\r');
            return newValue.Replace(match.Value, result);
        });
    }

    private static string ExecSync(string command, IDictionary<string, string> env)
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        ProcessStartInfo psi = new()
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(isWindows ? "/c" : "-c");
        psi.ArgumentList.Add(command);

        foreach (KeyValuePair<string, string> kvp in env)
        {
            psi.EnvironmentVariables[kvp.Key] = kvp.Value;
        }

        using Process process = Process.Start(psi)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception(string.IsNullOrWhiteSpace(stderr)
                ? $"Command failed with exit code {process.ExitCode}: {command}"
                : stderr.Trim());
        }

        return stdout;
    }

    [GeneratedRegex(@"\$\(([^)]+(?:\)[^(]*)*)\)")]
    private static partial Regex EvalRegex();
}