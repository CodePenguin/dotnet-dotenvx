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

/// <remarks>Reference source: dotenvx/src/cli/actions/set.js</remarks>
public class SetAction : Command
{
    private readonly Option<bool> _encryptOption =
        new("--encrypt", "-c") { Description = "encrypt value (default: true)" };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: same path as your env file)", HelpName = "path"
        };

    private readonly Argument<string> _keyArgument =
        new("key") { Description = "KEY", HelpName = "KEY" };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _plainOption =
        new("--plain") { Description = "store value as plain text (default: false)" };

    private readonly Option<bool> _noCreateOption =
        new("--no-create") { Description = "do not create env file if missing (default: false)" };

    private readonly Argument<string> _valueArgument =
        new("value") { Description = "value" };

    public SetAction(IDotenvxLogger logger) : base("set",
        "set a single environment variable")
    {
        _logger = logger;
        Add(_encryptOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_keyArgument);
        Add(_noCreateOption);
        Add(_plainOption);
                Add(_valueArgument);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string key = parseResult.GetValue(_keyArgument)!;
        string value = parseResult.GetValue(_valueArgument)!;
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        bool encrypt = parseResult.GetValue(_encryptOption);
        bool plain = parseResult.GetValue(_plainOption);
        bool noCreate = parseResult.GetValue(_noCreateOption);

        _logger.Debug($"key: {key}");
        _logger.Debug($"value: {value}");

        var options = new { envFile, envKeysFile, plain };
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        encrypt = encrypt || !plain;

        try
        {
            List<EnvItem> envItems = [];

            if (envFile != null)
            {
                envItems.AddRange(envFile.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
            }

            string? envKeysFilePath = envKeysFile?.FirstOrDefault();

            Sets setsService = Sets.Factory(key, value, envItems.ToArray(), encrypt, envKeysFilePath, noCreate);
            SetsResult result = setsService.Run();

            IList<ProcessedSet> processedEnvs = result.ProcessedEnvs;
            IList<string> changedFilePaths = result.ChangedFilePaths;
            IList<string> unchangedFilePaths = result.UnchangedFilePaths;

            string withEncryption = encrypt ? " with encryption" : "";

            int errorCount = 0;

            foreach (ProcessedSet processedEnv in processedEnvs)
            {
                _logger.Verbose($"setting for {processedEnv.EnvFilePath}");

                if (processedEnv.Error != null)
                {
                    errorCount++;
                    Exception? error = processedEnv.Error;
                    string code = (error as DotenvxException)?.Code ?? "UNKNOWN";
                    _logger.Warn(error.Message);
                    if (code == "MISSING_ENV_FILE")
                    {
                        _logger.Help(
                            $"? add one with [echo \"{key}=value\" > {processedEnv.EnvFilePath}] and re-run [dotenvx set]");
                    }
                    else
                    {
                        string? help = (error as DotenvxException)?.Help;
                        if (!string.IsNullOrEmpty(help))
                        {
                            _logger.Help(help);
                        }
                    }
                }
                else
                {
                    Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);

                    _logger.Verbose($"{processedEnv.Key} set{withEncryption} ({processedEnv.EnvFilePath})");
                    _logger.Debug(
                        $"{processedEnv.Key} set{withEncryption} to {processedEnv.Value} ({processedEnv.EnvFilePath})");
                }
            }

            if (changedFilePaths.Count > 0)
            {
                _logger.Success($"✔ set {key}{withEncryption} ({string.Join(',', changedFilePaths)})");
            }
            else if (unchangedFilePaths.Count > 0)
            {
                _logger.Info($"no changes ({string.Join(',', unchangedFilePaths)})");
            }

            foreach (ProcessedSet processedEnv in processedEnvs)
            {
                if (!processedEnv.PrivateKeyAdded)
                {
                    continue;
                }

                _logger.Success($"✔ key added to {processedEnv.EnvKeysFilePath} ({processedEnv.PrivateKeyName})");

                if (!IsIgnoringDotenvKeys())
                {
                    _logger.Help("⮕  next run [dotenvx ext gitignore --pattern .env.keys] to gitignore .env.keys");
                }

                _logger.Help(
                    $"⮕  next run [{processedEnv.PrivateKeyName}='{processedEnv.PrivateKey}' dotenvx get {key}] to test decryption locally");
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