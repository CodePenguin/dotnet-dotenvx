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

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/set.js. Fixed 2026-09-25: the success message used
/// "✔ set {key} with encryption"/"no changes" instead of the real "◈ encrypted {key}"/"◇ set
/// {key}"/"○ no change", and printed two entirely fabricated "✔ key added to .env.keys"/"⮕ next
/// run..." hint lines that don't exist anywhere in current OR older visible upstream source (not a
/// deprecated feature - never existed here at all). Also fixed the per-file loop to check
/// processedEnv.Changed (previously always treated a non-error result as "changed", skipping the
/// "no change" verbose line and unconditionally rewriting the file even when nothing changed).
/// </remarks>
public class SetAction : Command
{
    private readonly Option<bool> _encryptOption =
        new("--encrypt", "-c")
        {
            Description = "encrypt value (default: true)", DefaultValueFactory = _ => true
        };

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

        var options = new { envFile, envKeysFile, encrypt, plain };
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        // `-c`/`--encrypt` has no real "off" state upstream (commander registers it as a bare
        // boolean flag defaulting to true, with no --no-encrypt negation) - `--plain` is the only
        // thing that can ever turn encryption off, regardless of whether -c was explicitly passed.
        encrypt = !plain;

        try
        {
            List<EnvItem> envItems = [];
            envItems.AddRange(
                SplitCommaSeparatedEnvFiles(envFile).Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));

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
                    _logger.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
                }
                else if (processedEnv.Changed)
                {
                    Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);

                    _logger.Verbose($"{processedEnv.Key} set{withEncryption} ({processedEnv.EnvFilePath})");
                    _logger.Debug(
                        $"{processedEnv.Key} set{withEncryption} to {processedEnv.Value} ({processedEnv.EnvFilePath})");
                }
                else
                {
                    _logger.Verbose($"no change {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
            }

            if (changedFilePaths.Count > 0)
            {
                string msg = encrypt
                    ? $"◈ encrypted {key} ({string.Join(',', changedFilePaths)})"
                    : $"◇ set {key} ({string.Join(',', changedFilePaths)})";
                _logger.Success(msg);
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