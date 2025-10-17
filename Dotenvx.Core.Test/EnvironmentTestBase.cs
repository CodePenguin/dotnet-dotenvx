namespace Dotenvx.Core.Test;

public class EnvironmentTestBase : IDisposable
{
    // Store existing environment keys
    private readonly List<string> _existingEnvironmentKeys = Environment.GetEnvironmentVariables().Keys.OfType<string>().ToList();

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