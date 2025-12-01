using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

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

    private readonly string _originalDirectory = Environment.CurrentDirectory;
    protected MockLogger MockLogger { get; } = new();

    public void Dispose()
    {
        TearDown();
        if (Environment.CurrentDirectory != _originalDirectory)
        {
            Environment.CurrentDirectory = _originalDirectory;
        }

        // Remove any keys that were not in the original environment
        Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(key => !_existingEnvironmentKeys.Contains(key))
            .ToList()
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));

        // Reset Mock Factories
        Get.ResetFactory();
        KeyPair.ResetFactory();
        Ls.ResetFactory();
        Ops.ResetFactory();
        Parse.ResetFactory();
        Run.ResetFactory();
        Sets.ResetFactory();

        Fsx.ResetToDefaults();

        File.Delete(".env");
        File.Delete(".env.keys");
        GC.SuppressFinalize(this);
    }

    protected void ClearEnvironment()
    {
        _existingEnvironmentKeys
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));
    }

    protected void ClearEnvironmentExceptPath()
    {
        _existingEnvironmentKeys
            .FindAll(key => !key.Equals("PATH", StringComparison.InvariantCultureIgnoreCase))
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));
    }

    protected virtual void TearDown()
    {
        // override in descendents
    }
}