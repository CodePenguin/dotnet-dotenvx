using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <summary>What `prepareValidatedEnv` returns: the loaded rows and the outcome of the Envfile checks.</summary>
internal sealed class ValidatedEnv
{
    public required IDictionary<string, string> ProcessEnv { get; init; }
    public required RunProcessedEnv[] ProcessedEnvs { get; init; }
    public required string[] ReadableFilePaths { get; init; }
    public required HashSet<string> RequiredFilePaths { get; init; }
    public required bool HasEnvfile { get; init; }
    public required EnvfileSchema Schema { get; init; }
    public required IReadOnlyDictionary<string, string> Checked { get; init; }
    public InvalidEnvException? ValidationError { get; init; }
}

/// <summary>The options `prepareValidatedEnv` reads from the command (`options`) and its caller.</summary>
internal sealed class ValidateEnvOptions
{
    public string? Profile { get; init; }
    public string? Convention { get; init; }
    public bool Overload { get; init; }
    public string[]? EnvKeysFile { get; init; }
    public KeyAccessOptions KeyAccess { get; init; } = new();
    public bool No1Password { get; init; }
    public bool NoBitwarden { get; init; }
    public bool PromptForSecrets { get; init; }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/services/validate.js. Loads once and validates the final values against the Envfile;
/// callers own presentation and execution.
/// </remarks>
internal static class PrepareValidatedEnv
{
    public static ValidatedEnv Execute(EnvItem[] envs, ValidateEnvOptions options, IDictionary<string, string>? processEnv = null,
        bool requireEnvfile = true)
    {
        processEnv ??= EnvironmentHelper.GetEnvironmentVariablesAsDictionary();
        IDictionary<string, string?> nullable = processEnv.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        EnvItem[] explicitEnvs = options.Profile is not null ? envs : NormalizeDotenvConfigPath(envs, nullable);
        HashSet<string> required = [];
        foreach (EnvItem env in explicitEnvs.Where(env => env.Type == EnvType.EnvFile))
        {
            string resolved = ResolveDirectoryFilepath(env.Value, ".env");
            // A directory with a convention selects optional layers rather than one file.
            if (!(!string.IsNullOrEmpty(options.Convention) && resolved != env.Value))
            {
                required.Add(NodePath.Resolve(resolved));
            }
        }

        EnvItem[] selected = SelectEnvfileEnvs(envs,
            new SelectEnvfileOptions { Profile = options.Profile, Convention = options.Convention }, nullable);
        EnvfileSchema schema = ReadEnvfile.Read(null, selected.Where(env => env.Type == EnvType.EnvFile).Select(env => env.Value));
        if (requireEnvfile && !schema.Exists)
        {
            throw new EnvfileRequiredException();
        }

        Run run = Run.Factory(selected, options.Overload, processEnv, ResolveEnvKeysFile(options.EnvKeysFile));
        run.DetermineOnExecute = false;
        run.KeyAccess = options.KeyAccess;
        run.No1Password = options.No1Password;
        run.NoBitwarden = options.NoBitwarden;
        run.PromptForSecrets = options.PromptForSecrets;
        RunResult result = run.Execute();

        HashSet<string> keys = [.. schema.Keys.Keys, .. result.ProcessedEnvs.SelectMany(row => row.Parsed.Keys)];
        Dictionary<string, string> finalEnv = keys.Where(processEnv.ContainsKey).ToDictionary(key => key, key => processEnv[key]);
        CheckResult check = schema.Exists ? global::Dotenvx.Primitives.Primitives.Check(schema.Policy, finalEnv) : new CheckResult();

        return new ValidatedEnv
        {
            ProcessEnv = processEnv,
            ProcessedEnvs = result.ProcessedEnvs,
            ReadableFilePaths = result.ReadableFilePaths,
            RequiredFilePaths = required,
            HasEnvfile = schema.Exists,
            Schema = schema,
            Checked = check.Checked,
            ValidationError = ValidationError(check.Errors)
        };
    }

    /// <summary>`validateEnvfile.error(diagnostics)`.</summary>
    public static InvalidEnvException? ValidationError(IReadOnlyList<CheckError> diagnostics)
    {
        return diagnostics.Count == 0
            ? null
            : new InvalidEnvException(string.Join("; ", diagnostics.Select(item => item.Message)), diagnostics);
    }
}
