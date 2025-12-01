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

/// <remarks>Reference source: dotenvx/src/cli/actions/rotate.js</remarks>
public class RotateAction : Command
{
    private readonly Option<string[]?> _envFileOption =
        new("--env-file", "-f") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: same path as your env file)", HelpName = "path"
        };

    private readonly Option<string[]?> _excludeKeyOption =
        new("--exclude-key", "-ek")
        {
            Description = "keys(s) to exclude from encryption (default: none)", HelpName = "excludeKeys..."
        };

    private readonly Option<string[]?> _keyOption =
        new("--key", "-k") { Description = "keys(s) to encrypt (default: all keys in file)", HelpName = "keys..." };

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
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        // Build envs like in GetAction
        List<EnvItem> envItems = [];
        if (envFiles != null)
        {
            envItems.AddRange(envFiles.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
        }
        else
        {
            envItems.Add(new EnvItem { Type = EnvType.EnvFile, Value = ".env" });
        }

        if (stdout)
        {
            RotateResult result = Rotate.Factory(envItems.ToArray(), key, excludeKey, envKeysFile?.FirstOrDefault())
                .Run();

            foreach (RotateProcessedEnv processedEnv in result.ProcessedEnvs)
            {
                Console.WriteLine(processedEnv.EnvSrc);
                Console.WriteLine("");
                Console.WriteLine(processedEnv.EnvKeysSrc);
            }
        }
        else
        {
            try
            {
                Rotate rotate = Rotate.Factory(envItems.ToArray(), key, excludeKey, envKeysFile?.FirstOrDefault());
                RotateResult result = rotate.Run();

                foreach (RotateProcessedEnv processedEnv in result.ProcessedEnvs)
                {
                    _logger.Verbose($"rotating {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                    if (processedEnv.Error != null)
                    {
                        if (processedEnv.Error is DotenvxException { Code: "MISSING_ENV_FILE" })
                        {
                            _logger.Warn(processedEnv.Error.Message);
                            _logger.Help(
                                $"? add one with [echo \"HELLO=World\" > {processedEnv.EnvFilePath}] and re-run [dotenvx rotate]");
                        }
                        else
                        {
                            _logger.Warn(processedEnv.Error.Message);
                            if (processedEnv.Error is DotenvxException dotenvxException &&
                                !string.IsNullOrEmpty(dotenvxException.Help))
                            {
                                _logger.Help(dotenvxException.Help);
                            }
                        }
                    }
                    else if (processedEnv.Changed)
                    {
                        Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);
                        Fsx.WriteFileX(processedEnv.EnvKeysFilePath, processedEnv.EnvKeysSrc);

                        _logger.Verbose($"rotated {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                    }
                    else
                    {
                        _logger.Verbose($"no changes {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                    }
                }

                if (result.ChangedFilePaths.Length > 0)
                {
                    _logger.Success($"✔ rotated ({string.Join(',', result.ChangedFilePaths)})");
                }
                else if (result.UnchangedFilePaths.Length > 0)
                {
                    _logger.Info($"no changes ({string.Join(',', result.UnchangedFilePaths)})");
                }

                foreach (RotateProcessedEnv processedEnv in result.ProcessedEnvs)
                {
                    if (!processedEnv.PrivateKeyAdded)
                    {
                        continue;
                    }

                    _logger.Success($"✔ key added to .env.keys ({processedEnv.PrivateKeyName})");

                    if (!IsIgnoringDotenvKeys())
                    {
                        _logger.Help("⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
                    }

                    _logger.Help(
                        $"⮕  next run [{processedEnv.PrivateKeyName}='{processedEnv.PrivateKey}' dotenvx get] to test decryption locally");
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