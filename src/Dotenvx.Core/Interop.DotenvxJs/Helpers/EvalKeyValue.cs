using System.Text.RegularExpressions;

using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

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
                // Combine environment variables
                // ReSharper disable once CollectionNeverQueried.Local (Remove once Eval is implemented)
                Dictionary<string, string> envVars = new(processEnv);
                foreach (KeyValuePair<string, string> kvp in runningParsed)
                {
                    envVars[kvp.Key] = kvp.Value;
                }

                // TODO: Implement EvalKeyValue command execution.
                throw new NotSupportedException("Command Substitution is not supported.");
            }
            catch (Exception ex) when (ex is not DotenvxException)
            {
                throw new CommandSubstitutionFailed(key, command, ex.Message.Trim());
            }

            // Chomp the result and replace match with result
#pragma warning disable CS0162 // Remove once Eval is implemented
            result = result.TrimEnd('\n', '\r');
            return newValue.Replace(match.Value, result);
#pragma warning restore CS0162 // Remove once Eval is implemented
        });
    }

    [GeneratedRegex(@"\$\(([^)]+(?:\)[^(]*)*)\)")]
    private static partial Regex EvalRegex();
}