using System.Collections;

using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>The options of <see cref="Helpers.SelectEnvfileEnvs" />.</summary>
internal sealed class SelectEnvfileOptions
{
    public string? Profile { get; init; }
    public string? Convention { get; init; }

    /// <summary>`resolveDirectories: false` keeps directories as given.</summary>
    public bool ResolveDirectories { get; init; } = true;
}

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/selectEnvfileEnvs.js. Without an `Envfile` it is the old file selection
/// (DOTENV_FILE / DOTENV_PATH / DOTENV_F, then `.env`); with one, the profile of the Envfile picks the files, while explicit
/// `-f` files and `-e` strings keep their order.
/// </remarks>
internal static partial class Helpers
{
    internal static Dictionary<string, string?> CurrentProcessEnv()
    {
        return Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
    }

    public static EnvItem[] SelectEnvfileEnvs(EnvItem[]? envs, SelectEnvfileOptions? options = null,
        IDictionary<string, string?>? processEnv = null)
    {
        envs ??= [];
        options ??= new SelectEnvfileOptions();
        processEnv ??= CurrentProcessEnv();

        EnvItem[] Resolve(EnvItem[] values)
        {
            return options.ResolveDirectories ? BuildCommandEnvs(values, options.Convention) : values;
        }

        EnvfileSource document = ReadEnvfile.Source();
        if (!document.Exists)
        {
            if (options.Profile is not null)
            {
                throw new Exception("Profile selection requires an Envfile");
            }

            return DetermineEnvs(Resolve(NormalizeDotenvConfigPath(envs, processEnv)),
                processEnv.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!));
        }

        // Keep explicit CLI file/inline-env ordering; profiles only supply file selection.
        EnvItem[] explicitEnvs = Resolve(envs);
        EnvItem[] files = [.. explicitEnvs.Where(env => env.Type == EnvType.EnvFile)];
        string selected;
        try
        {
            selected = Primitives.Primitives.Profile(document.Source!, new ProfileOptions
            {
                F = files.Length > 0 ? string.Join(',', files.Select(env => env.Value)) : null,
                Profile = options.Profile,
                ProcessEnv = processEnv.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!)
            });
        }
        catch (MalformedEnvfileException error)
        {
            throw ReadEnvfile.FormatError(error, document);
        }

        if (files.Length > 0)
        {
            return explicitEnvs;
        }

        return Resolve(
        [
            .. selected.Length > 0
                ? selected.Split(',').Select(value => new EnvItem { Type = EnvType.EnvFile, Value = value })
                : [],
            .. explicitEnvs
        ]);
    }
}
