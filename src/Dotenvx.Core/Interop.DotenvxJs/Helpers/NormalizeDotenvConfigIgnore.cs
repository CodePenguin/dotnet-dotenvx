using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/normalizeDotenvConfigIgnore.js</remarks>
public static partial class Helpers
{
    public static ConfigOptions NormalizeDotenvConfigIgnore(ConfigOptions options)
    {
        string configuredIgnore = Environment.GetEnvironmentVariable("DOTENV_IGNORE") ??
                                   Environment.GetEnvironmentVariable("DOTENV_CONFIG_IGNORE") ?? "";
        List<string> configIgnore = configuredIgnore
            .Split(',')
            .Select(code => code.Trim())
            .Where(code => code.Length > 0)
            .ToList();

        if (configIgnore.Count < 1)
        {
            return options;
        }

        return new ConfigOptions
        {
            Logger = options.Logger,
            Path = options.Path,
            Overload = options.Overload,
            ProcessEnv = options.ProcessEnv,
            Ignore = [..options.Ignore ?? [], ..configIgnore],
            Strict = options.Strict,
            EnvKeysFile = options.EnvKeysFile,
            Convention = options.Convention,
            Debug = options.Debug,
            Quiet = options.Quiet,
            Verbose = options.Verbose,
            LogLevel = options.LogLevel
        };
    }
}
