namespace dotnet_dotenvx.Test;

public abstract class EnvironmentTestBase : IDisposable
{
    // Store existing environment keys
    private readonly List<string> _existingEnvironmentKeys =
        Environment.GetEnvironmentVariables().Keys.OfType<string>().ToList();

    private readonly string _originalDirectory = Environment.CurrentDirectory;

    public void Dispose()
    {
        if (Environment.CurrentDirectory != _originalDirectory)
        {
            Environment.CurrentDirectory = _originalDirectory;
        }

        // Remove any keys that were not in the original environment
        Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(key => !_existingEnvironmentKeys.Contains(key))
            .ToList()
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));

        File.Delete(".env");
        GC.SuppressFinalize(this);
    }

    protected void ClearEnvironment()
    {
        _existingEnvironmentKeys.ForEach(key => Environment.SetEnvironmentVariable(key, null));
    }
}