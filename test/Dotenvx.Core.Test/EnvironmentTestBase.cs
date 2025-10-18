namespace Dotenvx.Core.Test;

public class EnvironmentTestBase : IDisposable
{
    // Store existing environment keys
    private readonly List<string> _existingEnvironmentKeys = Environment.GetEnvironmentVariables().Keys.OfType<string>().ToList();

    /*
    .NET 8.0 removes the environment variables when set to blank.
    Expected behavior changes after .NET 9.0 https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable
    */
    protected const string? EmptyEnvironmentVariableValue = null;
    
    public void Dispose()
    {
        // Remove any keys that were not in the original environment
        Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(key => !_existingEnvironmentKeys.Contains(key))
            .ToList()
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));
        GC.SuppressFinalize(this);
    }
}