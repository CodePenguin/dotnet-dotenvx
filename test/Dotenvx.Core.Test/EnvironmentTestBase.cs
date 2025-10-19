using Dotenvx.Core.Interop.DotenvxJs;
using Dotenvx.Core.Interop.DotenvxJs.Helpers;
using Dotenvx.Core.Interop.DotenvxJs.Services;

namespace Dotenvx.Core.Test;

public class EnvironmentTestBase : IDisposable
{
    /*
    .NET 8.0 removes the environment variables when set to blank.
    Expected behavior changes after .NET 9.0 https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable
    */
    protected const string? EmptyEnvironmentVariableValue = null;

    // Store existing environment keys
    private readonly List<string> _existingEnvironmentKeys =
        Environment.GetEnvironmentVariables().Keys.OfType<string>().ToList();

    public void Dispose()
    {
        // Remove any keys that were not in the original environment
        Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(key => !_existingEnvironmentKeys.Contains(key))
            .ToList()
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));

        // Reset Mock Factories
        Main.RunFactory = (envs, overload, envKeysFile, processEnv)
            => new Run(envs, overload, envKeysFile, processEnv);
        Main.ParseFactory = (src, privateKey, processEnv, overload, privateKeyName)
            => new Parse(src, privateKey, processEnv, overload, privateKeyName);

        GC.SuppressFinalize(this);
    }
}