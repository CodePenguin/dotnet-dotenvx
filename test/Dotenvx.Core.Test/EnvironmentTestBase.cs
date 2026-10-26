using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

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
        _existingEnvironmentKeys = [.. _originalEnvironmentVariables.Keys];

        // unit tests never read the developer's real Armor session or device key (the directory has no settings file, so
        // Armor is off) ...
        Environment.SetEnvironmentVariable("DOTENVX_CONFIG",
            Path.Combine(Path.GetTempPath(), "dotenvx-unit-config-" + Guid.NewGuid().ToString("N")));

        _existingEnvironmentKeys.Add("DOTENVX_CONFIG"); // ClearEnvironment() removes it with the rest

        // ... and never reach the real OS secret store: the stand-in is an unavailable store, so keys fall back to files
        INativeSecretStore unavailable = NSubstitute.Substitute.For<INativeSecretStore>();
        NSubstitute.SubstituteExtensions.When(unavailable, store => store.Set(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>()))
            .Do(_ => throw new global::Dotenvx.Providers.NativeStoreException("NATIVE_UNAVAILABLE", "OS secret store unavailable"));
        NativeStore.Current = unavailable;

        // colour depth is decided from the environment and platform: tests see a plain non-Windows terminal, whatever the dev shell is
        ConsoleTty.Platform = new global::Dotenvx.Interop.DotenvxJs.Helpers.OsPlatform(false, 0, 0);
        foreach (string name in new[] { "FORCE_COLOR", "NO_COLOR", "NODE_DISABLE_COLORS", "TMUX", "CI", "TF_BUILD", "TEAMCITY_VERSION", "TERM_PROGRAM", "COLORTERM" })
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    public virtual void Dispose()
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
        ArmorUp.ResetFactory();
        ArmorPush.ResetFactory();
        ArmorInvite.ResetToDefaults();
        ArmorInvite.Factory = (host, token, team, email) => new ArmorInvite(host, token, team, email);
        KeyStorage.ResetToDefaults();
        SecretCli.ResetToDefaults();
        NativeStore.ResetToDefaults();
        Session.ResetToDefaults();
        ArmorHttp.ResetToDefaults();
        ExecuteDynamic.ResetToDefaults();
        OpenUrl.ResetToDefaults();
        PtyCommand.ResetToDefaults();
        ParseWithDecryptor.ResetToDefaults();
        Spinners.ResetToDefaults();
        ArmorProviders.ResetToDefaults();
        KeyProviders.ResetToDefaults();
        ListenForOpenKey.ResetToDefaults();
        SystemInformation.ResetToDefaults();
        EnvPaths.ConfigOverride = null;
        KeychainUp.ResetFactory();
        KeychainDown.ResetFactory();
        KeychainPush.ResetFactory();
        KeychainPull.ResetFactory();
        Prompts.ResetToDefaults();
        Doctor.ResetFactory();
        Genexample.ResetFactory();
        Get.ResetFactory();
        Gitignore.ResetFactory();
        KeyPair.ResetFactory();
        Ls.ResetFactory();
        Prebuild.ResetFactory();
        Precommit.ResetFactory();
        Run.ResetFactory();
        Sets.ResetFactory();

        Fsx.ResetToDefaults();
        GitHelper.ResetToDefaults();
        GitleaksHelper.ResetToDefaults();
        ConsoleTty.ResetToDefaults();

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
