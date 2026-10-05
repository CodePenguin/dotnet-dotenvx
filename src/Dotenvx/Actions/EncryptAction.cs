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
/// Reference source: dotenvx/src/cli/actions/encrypt.js. Fixed 2026-09-25: the success message
/// used "✔ encrypted"/"no changes" instead of the real "◈ encrypted"/"○ no change", and printed
/// two entirely fabricated "✔ key added to .env.keys"/"⮕ next run..." hint lines - current
/// encrypt.js has ONLY commented-out (`// logger.success(...)`) code for an "· armored" suffix, no
/// separate hint lines at all, confirming these were never real. Also fixed the per-file loop's
/// "no change" branch to be unconditional verbose logging (matching `} else { logger.verbose(...)
/// }` exactly), not gated on an unrelated `unchangedFilepaths.Count > 0` check.
/// </remarks>
public class EncryptAction : Command
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

    private readonly Option<bool> _noCreateOption =
        new("--no-create") { Description = "do not create .env file(s) when missing" };

    private readonly Option<bool> _stdoutOption =
        new("--stdout") { Description = "send to stdout" };

    public EncryptAction(IDotenvxLogger logger) : base("encrypt",
        "encrypt .env file(s)")
    {
        Aliases.Add("enc"); // shorthand
        _logger = logger;
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_keyOption);
        Add(_excludeKeyOption);
        Add(_stdoutOption);
        Add(_noCreateOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        string[]? key = parseResult.GetValue(_keyOption);
        string[]? excludeKey = parseResult.GetValue(_excludeKeyOption);
        bool stdout = parseResult.GetValue(_stdoutOption);
        bool noCreate = parseResult.GetValue(_noCreateOption);

        var options = new
        {
            envFile,
            envKeysFile,
            key,
            excludeKey,
            stdout,
            noCreate
        };
        _logger.Debug($"options: {JsonStringify(options)}");

        List<EnvItem> envItems = [];
        envItems.AddRange(
            SplitCommaSeparatedEnvFiles(envFile).Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));

        string? envKeysFilePath = envKeysFile?.LastOrDefault(); // plain `<path>` option: commander keeps the LAST occurrence

        // stdout - should not have a try so that exit codes can surface to stdout
        if (stdout)
        {
            Encrypt encryptService = new(envItems.ToArray(), key, excludeKey, envKeysFilePath, noCreate);
            EncryptResult result = encryptService.Run();

            int stdoutErrorCount = 0;
            foreach (EncryptProcessedEnv processedEnv in result.ProcessedEnvs)
            {
                if (processedEnv.Error != null)
                {
                    stdoutErrorCount++;
                    _logger.Error((processedEnv.Error as DotenvxException)?.MessageWithHelp ?? processedEnv.Error.Message);
                }
                else if (!string.IsNullOrEmpty(processedEnv.EnvSrc))
                {
                    Console.WriteLine(processedEnv.EnvSrc);
                }
            }

            return stdoutErrorCount > 0 ? 1 : 0; // exit early
        }

        try
        {
            Encrypt encryptService = new(envItems.ToArray(), key, excludeKey, envKeysFilePath, noCreate);
            EncryptResult result = encryptService.Run();

            if (!string.IsNullOrEmpty(result.KeysSrc))
            {
                Fsx.WriteFileX(envKeysFilePath ?? ".env.keys", result.KeysSrc);
            }

            IList<EncryptProcessedEnv> processedEnvs = result.ProcessedEnvs;
            IList<string> changedFilePaths = result.ChangedFilePaths;
            IList<string> unchangedFilePaths = result.UnchangedFilePaths;
            int errorCount = 0;

            foreach (EncryptProcessedEnv processedEnv in processedEnvs)
            {
                _logger.Verbose($"encrypting {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                if (processedEnv.Error != null)
                {
                    errorCount++;
                    Exception? error = processedEnv.Error;
                    _logger.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
                }
                else if (processedEnv.Changed)
                {
                    Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);

                    _logger.Verbose($"encrypted {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
                else
                {
                    _logger.Verbose($"no change {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
            }

            if (changedFilePaths.Count > 0)
            {
                _logger.Success($"◈ encrypted ({string.Join(',', changedFilePaths)})");
            }
            else if (unchangedFilePaths.Count > 0)
            {
                _logger.Info($"○ no change ({string.Join(',', unchangedFilePaths)})");
            }

            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            return 1;
        }
    }
}