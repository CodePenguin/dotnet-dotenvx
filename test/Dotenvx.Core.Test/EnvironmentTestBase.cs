using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

namespace Dotenvx.Core.Test;

public class EnvironmentTestBase : IDisposable
{
    /*
    .NET 8.0 removed environment variables when set to blank; since .NET 9.0 they are kept as "".
    https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable
    Now that the solution targets net10.0, this reflects current (.NET 9+) behavior.
    */
    protected const string EmptyEnvironmentVariableValue = "";

    // Snapshot of the environment as it existed before this test, so it can be fully restored afterward
    // (ClearEnvironment/ClearEnvironmentExceptPath remove variables for the rest of the process otherwise,
    // since nothing else in this class puts their original values back).
    private readonly Dictionary<string, string?> _originalEnvironmentVariables = Environment
        .GetEnvironmentVariables()
        .Cast<System.Collections.DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);

    private readonly List<string> _existingEnvironmentKeys;

    private readonly string _originalDirectory = Environment.CurrentDirectory;
    protected MockLogger MockLogger { get; } = new();

    protected EnvironmentTestBase()
    {
        _existingEnvironmentKeys = _originalEnvironmentVariables.Keys.ToList();
    }

    public void Dispose()
    {
        TearDown();
        if (Environment.CurrentDirectory != _originalDirectory)
        {
            Environment.CurrentDirectory = _originalDirectory;
        }

        // Remove any keys that were not in the original environment
        Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(key => !_originalEnvironmentVariables.ContainsKey(key))
            .ToList()
            .ForEach(key => Environment.SetEnvironmentVariable(key, null));

        // Restore original values for keys that were cleared or modified during the test
        foreach (KeyValuePair<string, string?> kv in _originalEnvironmentVariables)
        {
            if (Environment.GetEnvironmentVariable(kv.Key) != kv.Value)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }

        // Reset Mock Factories
        Del.ResetFactory();
        Get.ResetFactory();
        KeyPair.ResetFactory();
        Ls.ResetFactory();
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