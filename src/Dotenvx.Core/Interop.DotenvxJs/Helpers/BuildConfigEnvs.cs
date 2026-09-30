using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/buildConfigEnvs.js. Routes Config()'s env-item
/// building through the same directory-resolution logic (BuildCommandEnvs/ResolveDirectoryFilepath)
/// that run/get/validate's CLI actions already use - `{ ...options, convention: undefined }` before
/// calling buildEnvs, so the convention isn't applied twice (once by buildEnvs' own shorthand
/// handling, once by buildCommandEnvs). Found missing 2026-09-29 via a code audit: Main.Config()
/// called plain BuildEnvs directly, so a directory path (e.g. `./configdir`) never resolved to
/// `./configdir/.env` the way the CLI commands already correctly do for the same input shape.
/// </remarks>
public static partial class Helpers
{
    public static EnvItem[] BuildConfigEnvs(ConfigOptions options)
    {
        ConfigOptions pathOptions = new()
        {
            Logger = options.Logger,
            Path = options.Path,
            Overload = options.Overload,
            ProcessEnv = options.ProcessEnv,
            Ignore = options.Ignore,
            Strict = options.Strict,
            EnvKeysFile = options.EnvKeysFile,
            Convention = null,
            Debug = options.Debug,
            Quiet = options.Quiet,
            Verbose = options.Verbose,
            LogLevel = options.LogLevel,
            Mask = options.Mask
        };

        return BuildCommandEnvs(BuildEnvs(pathOptions), options.Convention);
    }
}
