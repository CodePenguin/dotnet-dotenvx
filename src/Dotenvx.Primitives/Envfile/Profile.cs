namespace Dotenvx.Primitives;

/// <summary>The options of <see cref="Primitives.Profile" />.</summary>
public sealed class ProfileOptions
{
    /// <summary>An explicit profile name.</summary>
    public string? Profile { get; init; }

    /// <summary>Explicit comma-separated files; they win over a profile name.</summary>
    public string? F { get; init; }

    public IReadOnlyDictionary<string, string>? ProcessEnv { get; init; }

    /// <summary>The variables that name a profile, in order.</summary>
    public IReadOnlyList<string>? EnvironmentKeys { get; init; }
}

/// <remarks>
/// Counterpart of the @dotenvx/primitives `profile` export. Selection order: explicit files, an explicit profile,
/// DOTENV_FILE / DOTENV_PATH / DOTENV_F, then the first set of the environment keys (DOTENV_ENV, APP_ENV, RAILS_ENV,
/// RACK_ENV, MIX_ENV, NODE_ENV); with no selector the `development` profile if declared, otherwise `.env`.
/// </remarks>
public static partial class Primitives
{
    private static readonly string[] ProfileFileKeys = ["DOTENV_FILE", "DOTENV_PATH", "DOTENV_F"];
    private static readonly string[] ProfileEnvironmentKeys = ["DOTENV_ENV", "APP_ENV", "RAILS_ENV", "RACK_ENV", "MIX_ENV", "NODE_ENV"];

    public static string Profile(string source, ProfileOptions? options = null)
    {
        options ??= new ProfileOptions();
        EnvfileModel model = LoadEnvfile(source);
        IReadOnlyDictionary<string, string> processEnv = options.ProcessEnv ?? new Dictionary<string, string>();
        IReadOnlyList<string> environmentKeys = options.EnvironmentKeys ?? ProfileEnvironmentKeys;
        if (environmentKeys.Any(key => key.Length == 0))
        {
            throw new ArgumentException("environmentKeys must be an array of variable names");
        }

        List<string> ByName(string name)
        {
            if (JsTrim(name).Length == 0)
            {
                throw new ArgumentException("Profile name must be a nonempty string");
            }

            return model.Profiles.TryGetValue(name, out List<string>? paths)
                ? paths
                : throw new MalformedEnvfileException($"No profile declaration for name: {name}");
        }

        string? FirstSet(IEnumerable<string> keys)
        {
            string? key = keys.FirstOrDefault(candidate => processEnv.TryGetValue(candidate, out string? value) && value != "");
            return key == null ? null : processEnv[key];
        }

        List<string> files;
        if (options.F != null)
        {
            files = SplitFiles(options.F);
        }
        else if (options.Profile != null)
        {
            files = ByName(options.Profile);
        }
        else if (FirstSet(ProfileFileKeys) is { } explicitFiles)
        {
            files = SplitFiles(explicitFiles);
        }
        else if (FirstSet(environmentKeys) is { } name)
        {
            files = ByName(name);
        }
        else
        {
            files = model.Profiles.TryGetValue("development", out List<string>? development) ? development : [".env"];
        }

        HashSet<string> seen = [];
        return string.Join(',', files.Where(file => seen.Add(model.Normalize(file))));
    }
}
