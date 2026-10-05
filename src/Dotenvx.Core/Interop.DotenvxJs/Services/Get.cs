using System.Collections;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/resolvers/get.js (moved there from src/lib/services/get.js,
/// which no longer exists at this pin - confirmed 2026-09-24, part of the same custodian/keyring
/// reorg CLAUDE.md already documents elsewhere).
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class Get(string? key, EnvItem[] envs, bool overload, bool all, string[]? envKeysFilePaths)
{
    private static readonly Func<string?, EnvItem[], bool, bool, string[]?, Get> DefaultFactory =
        (key, envs, overload, all, envKeysFile) => new Get(key, envs, overload, all, envKeysFile);

    public static Func<string?, EnvItem[], bool, bool, string[]?, Get>
        Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    /// <summary>resolvers/get.js passes no1Password / noBitwarden through to the envs resolver.</summary>
    public bool No1Password { get; set; }

    /// <summary>How missing private keys are looked up: --no-native, --no-armor, --token (providers/index.js options).</summary>
    public KeyAccessOptions KeyAccess { get; set; } = new();

    public bool NoNative
    {
        get => KeyAccess.NoNative;
        set => KeyAccess.NoNative = value;
    }

    public bool NoBitwarden { get; set; }

    public bool PromptForSecrets { get; set; }

    public virtual GetResult Run()
    {
        Dictionary<string, string> processEnv = new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty));
        Run runService = Services.Run.Factory(envs, overload, processEnv, envKeysFilePaths);
        runService.KeyAccess = KeyAccess;
        runService.No1Password = No1Password;
        runService.NoBitwarden = NoBitwarden;
        runService.PromptForSecrets = PromptForSecrets;
        RunResult runResult = runService.Execute();

        List<Exception> errors = [];
        foreach (RunProcessedEnv processedEnv in runResult.ProcessedEnvs)
        {
            errors.AddRange(processedEnv.Errors);
        }

        if (!string.IsNullOrEmpty(key)) // resolvers/get.js: `if (key)`, an empty key is the no-key branch
        {
            Dictionary<string, string> parsed = [];
            if (processEnv.TryGetValue(key, out string? value))
            {
                parsed[key] = value;
            }
            else
            {
                errors.Add(new MissingKeyException(key));
            }

            return new GetResult { Parsed = parsed, Errors = errors.ToArray() };
        }
        else
        {
            if (all)
            {
                return new GetResult { Parsed = processEnv, Errors = errors.ToArray() };
            }

            Dictionary<string, string> parsed = [];
            foreach (RunProcessedEnv processedEnv in runResult.ProcessedEnvs)
            {
                if (processedEnv.Parsed.Count <= 0)
                {
                    continue;
                }

                foreach (string parsedKey in processedEnv.Parsed.Keys)
                {
                    if (processEnv.TryGetValue(parsedKey, out string? value))
                    {
                        parsed[parsedKey] = value;
                    }
                }
            }

            return new GetResult { Parsed = parsed, Errors = errors.ToArray() };
        }
    }
}