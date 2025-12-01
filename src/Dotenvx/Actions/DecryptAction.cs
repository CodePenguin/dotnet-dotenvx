using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/decrypt.js</remarks>
public class DecryptAction : Command
{
    private readonly Option<string[]?> _envFileOption =
        new("--env-file", "-f") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: all keys in file)", HelpName = "path"
        };

    private readonly Option<string[]?> _excludeKeyOption =
        new("--exclude-key", "-ek")
        {
            Description = "keys(s) to exclude from decryption (default: none)", HelpName = "excludeKeys..."
        };

    private readonly Option<string[]?> _keysOption =
        new("--key", "-k") { Description = "keys(s) to decrypt (default: all keys in file)", HelpName = "keys..." };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _stdoutOption =
        new("--stdout") { Description = "send to stdout" };

    public DecryptAction(IDotenvxLogger logger) : base("decrypt",
        "convert encrypted .env file(s) to plain .env file(s)")
    {
        _logger = logger;
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_excludeKeyOption);
        Add(_keysOption);
        Add(_stdoutOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        string[]? excludeKey = parseResult.GetValue(_excludeKeyOption);
        string[]? keys = parseResult.GetValue(_keysOption);
        bool stdout = parseResult.GetValue(_stdoutOption);

        var options = new
        {
            envFile,
            envKeysFile,
            excludeKey,
            keys,
            stdout
        };
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        List<EnvItem> envItems = [];

        if (envFile != null)
        {
            envItems.AddRange(envFile.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
        }

        string? envKeysFilePath = envKeysFile?.FirstOrDefault();
        int errorCount = 0;

        // stdout - should not have a try so that exit codes can surface to stdout
        if (stdout)
        {
            Decrypt decryptService = new(envItems.ToArray(), keys, excludeKey, envKeysFilePath);
            DecryptResult result = decryptService.Run();

            foreach (ProcessedDecrypt processedEnv in result.ProcessedEnvs)
            {
                if (processedEnv.Error is not null)
                {
                    errorCount += 1;
                    _logger.Error(processedEnv.Error.Message);
                }
                else
                {
                    Console.WriteLine(processedEnv.EnvSrc);
                }
            }

            return errorCount > 0 ? 1 : 0; // exit early
        }

        try
        {
            Decrypt decryptService = new(envItems.ToArray(), null, excludeKey, envKeysFilePath);
            DecryptResult result = decryptService.Run();

            IList<ProcessedDecrypt> processedEnvs = result.ProcessedEnvs;
            IList<string> changedFilePaths = result.ChangedFilePaths;
            IList<string> unchangedFilePaths = result.UnchangedFilePaths;

            foreach (ProcessedDecrypt processedEnv in processedEnvs)
            {
                _logger.Verbose($"decrypting {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                if (processedEnv.Error != null)
                {
                    errorCount += 1;
                    Exception? error = processedEnv.Error;
                    string code = (error as DotenvxException)?.Code ?? "UNKNOWN";
                    _logger.Error(error.Message);
                    if (code == "MISSING_ENV_FILE")
                    {
                        _logger.Help(
                            $"? add one with [echo \"HELLO=World\" > {processedEnv.EnvFilePath}] and re-run [Dotenvx decrypt]");
                    }
                    else
                    {
                        string? help = (error as DotenvxException)?.Help;
                        if (!string.IsNullOrEmpty(help))
                        {
                            _logger.Error(help);
                        }
                    }
                }
                else if (processedEnv.Changed)
                {
                    Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);

                    _logger.Verbose($"decrypted {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
                else
                {
                    _logger.Info($"no changes {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
            }

            if (changedFilePaths.Count > 0)
            {
                _logger.Success($"✔ decrypted ({string.Join(',', changedFilePaths)})");
            }
            else if (unchangedFilePaths.Count > 0)
            {
                _logger.Info($"no changes ({string.Join(',', unchangedFilePaths)})");
            }

            return 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            return 1;
        }
    }
}