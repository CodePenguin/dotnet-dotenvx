using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/rotate.js AT COMMIT f1ffd157 (2026-06-29), the last
/// revision before it was deleted in 8af55792 ("remove rotate for 2.0. to return in more complete
/// fashion for 2.1") - `rotate` doesn't exist at all at this port's v2.24.1 pin (confirmed via
/// `grep rotate dotenvx.js` - no hits), and Rotate.cs's own doc comment previously said "there is
/// deliberately no current upstream file this comment could point to" - true for the PIN, but a
/// real historical version does exist in the submodule's own git history, found 2026-09-26.
///
/// Fixed against that historical source, several real bugs this action had presumably always had
/// (this file was likely modeled on encrypt.js/set.js's shape rather than rotate.js's own, before
/// this historical file was known to exist): the success symbol was "✔ rotated" instead of the
/// real "⟳ rotated"; "no changes" instead of "○ no change" (both the per-file Verbose line and the
/// summary Info line); two entirely fabricated "✔ key added to .env.keys"/"⮕  next run..." hint
/// lines with no basis in the historical source either; a fabricated custom
/// "? add one with [echo ...] and re-run [dotenvx rotate]" MISSING_ENV_FILE help override (the
/// historical source just does `logger.warn(processedEnv.error.messageWithHelp)`, one combined
/// call, matching this session's earlier systemic error-printing fix - this action was evidently
/// never updated when that fix landed elsewhere); and the `--stdout` branch unconditionally printed
/// the blank line + `.env.keys` content for every file, when the historical source only does that
/// `if (processedEnv.localPrivateKeyAdded)` - same conditional now applied to the non-stdout
/// branch's `.env.keys` write, which was also unconditional before.
/// </remarks>
public class RotateAction : Command
{
    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: same path as your env file)", HelpName = "path"
        };

    private readonly Option<string[]?> _excludeKeyOption =
        new("--exclude-key", "-ek")
        {
            Description = "keys(s) to exclude from encryption (default: none)", HelpName = "excludeKeys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string[]?> _keyOption =
        new("--key", "-k")
        {
            Description = "keys(s) to encrypt (default: all keys in file)", HelpName = "keys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _stdoutOption =
        new("--stdout") { Description = "send to stdout" };

    public RotateAction(IDotenvxLogger logger) : base("rotate",
        "rotate keypair(s) and re-encrypt .env file(s)")
    {
        _logger = logger;
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_excludeKeyOption);
        Add(_keyOption);
        Add(_stdoutOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string[]? envFiles = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        string[]? excludeKey = parseResult.GetValue(_excludeKeyOption);
        string[]? key = parseResult.GetValue(_keyOption);
        bool stdout = parseResult.GetValue(_stdoutOption);

        var options = new
        {
            envFile = envFiles,
            envKeysFile,
            excludeKey,
            key,
            stdout
        };
        _logger.Debug($"options: {JsonStringify(options)}");

        // Build envs like in GetAction
        List<EnvItem> envItems = [];
        string[] splitEnvFiles = SplitCommaSeparatedEnvFiles(envFiles);
        if (splitEnvFiles.Length > 0)
        {
            envItems.AddRange(splitEnvFiles.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
        }
        else
        {
            envItems.Add(new EnvItem { Type = EnvType.EnvFile, Value = ".env" });
        }

        if (stdout)
        {
            RotateResult result = Rotate.Factory(envItems.ToArray(), key, excludeKey, envKeysFile?.LastOrDefault())
                .Run();

            foreach (RotateProcessedEnv processedEnv in result.ProcessedEnvs)
            {
                Console.WriteLine(processedEnv.EnvSrc);
                if (processedEnv.PrivateKeyAdded)
                {
                    Console.WriteLine("");
                    Console.WriteLine(processedEnv.EnvKeysSrc);
                }
            }
        }
        else
        {
            try
            {
                Rotate rotate = Rotate.Factory(envItems.ToArray(), key, excludeKey, envKeysFile?.LastOrDefault());
                RotateResult result = rotate.Run();

                foreach (RotateProcessedEnv processedEnv in result.ProcessedEnvs)
                {
                    _logger.Verbose($"rotating {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                    if (processedEnv.Error != null)
                    {
                        Exception error = processedEnv.Error;
                        _logger.Warn((error as DotenvxException)?.MessageWithHelp ?? error.Message);
                    }
                    else if (processedEnv.Changed)
                    {
                        Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);
                        if (processedEnv.PrivateKeyAdded)
                        {
                            Fsx.WriteFileX(processedEnv.EnvKeysFilePath, processedEnv.EnvKeysSrc);
                        }

                        _logger.Verbose($"rotated {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                    }
                    else
                    {
                        _logger.Verbose($"no change {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                    }
                }

                if (result.ChangedFilePaths.Length > 0)
                {
                    _logger.Success($"⟳ rotated ({string.Join(',', result.ChangedFilePaths)})");
                }
                else if (result.UnchangedFilePaths.Length > 0)
                {
                    _logger.Info($"○ no change ({string.Join(',', result.UnchangedFilePaths)})");
                }
            }
            catch (Exception error)
            {
                CatchAndLog(_logger, error);
                return 1;
            }
        }

        return 0;
    }
}