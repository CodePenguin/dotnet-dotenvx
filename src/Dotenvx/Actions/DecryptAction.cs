using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Events;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/decrypt.js. Fixed 2026-09-25: the success message
/// used "✔ decrypted"/"no changes" instead of the real "◇ decrypted"/"○ no change", and the
/// per-file "no change" line was logged at Info level instead of Verbose (decrypt.js's own
/// `logger.verbose('no change ...')`) - so it printed unconditionally on every decrypt, not just
/// with --verbose.
/// </remarks>
public class DecryptAction : Command
{
    private readonly Option<string[]?> _envFileOption =
        new("--file", "-f", "--env-file") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path(s) to your .env.keys file(s) (default: same path as your env file)", HelpName = "path"
        };

    private readonly Option<string[]?> _excludeKeyOption =
        new("--exclude-key", "-ek")
        {
            Description = "keys(s) to exclude from decryption (default: none)", HelpName = "excludeKeys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string[]?> _keysOption =
        new("--key", "-k")
        {
            Description = "keys(s) to decrypt (default: all keys in file)", HelpName = "keys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _noArmorOption = SecretResolverOptions.NoArmor();

    private readonly Option<bool> _noNativeOption = SecretResolverOptions.NoNative();

    private readonly Option<bool> _stdoutOption =
        new("--stdout") { Description = "send to stdout" };

    private readonly Option<string?> _maskOption =
        new("--mask")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "mask stdout values, optionally setting visible characters",
            HelpName = "characters"
        };

    public DecryptAction(IDotenvxLogger logger) : base("decrypt",
        "decrypt .env file(s)")
    {
        Aliases.Add("dec"); // shorthand
        _logger = logger;
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_excludeKeyOption);
        Add(_keysOption);
        Add(_stdoutOption);
        Add(_maskOption);
        Add(_noNativeOption);
        Add(_noArmorOption);
        SetAction(Execute);
        EventTracking.Track(this, "decrypt");
    }

    private int Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "decrypting");

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
        _logger.Debug($"options: {JsonStringify(Help.CommanderOpts.Build(parseResult))}");

        List<EnvItem> envItems = [];
        envItems.AddRange(
            SplitCommaSeparatedEnvFiles(envFile).Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));

        string[]? envKeysFilePath = envKeysFile is { Length: > 0 } ? envKeysFile : null; // decrypt passes `fk` straight through (no directory resolution)
        int errorCount = 0;

        // stdout - should not have a try so that exit codes can surface to stdout
        if (stdout)
        {
            Decrypt decryptService = new(envItems.ToArray(), keys, excludeKey, envKeysFilePath)
            {
                KeyAccess = SecretResolverOptions.KeyAccess(_logger, parseResult.GetValue(_noNativeOption), parseResult.GetValue(_noArmorOption), null, SecretResolverOptions.ArmorMode.Session,
                spinner.OnStatus)
            };
            DecryptResult result = decryptService.Run();

            foreach (ProcessedDecrypt processedEnv in result.ProcessedEnvs)
            {
                if (processedEnv.Error is not null)
                {
                    errorCount += 1;
                    _logger.Error((processedEnv.Error as DotenvxException)?.MessageWithHelp ?? processedEnv.Error.Message);
                }
                else
                {
                    string envSrc = processedEnv.EnvSrc;
                    System.CommandLine.Parsing.OptionResult? maskResult = parseResult.GetResult(_maskOption);
                    if (maskResult != null)
                    {
                        object? showChar = parseResult.GetValue(_maskOption);
                        if (showChar is null)
                        {
                            showChar = 6;
                        }

                        envSrc = MaskEnvSrc(envSrc, showChar);
                    }

                    Spinners.StopActive();

                    Console.WriteLine(envSrc);
                }
            }

            EventScope.Count(errorCount);

            return errorCount > 0 ? 1 : 0; // exit early
        }

        try
        {
            Decrypt decryptService = new(envItems.ToArray(), keys, excludeKey, envKeysFilePath)
            {
                KeyAccess = SecretResolverOptions.KeyAccess(_logger, parseResult.GetValue(_noNativeOption), parseResult.GetValue(_noArmorOption), null, SecretResolverOptions.ArmorMode.Session,
                spinner.OnStatus)
            };
            DecryptResult result = decryptService.Run();

            IList<ProcessedDecrypt> processedEnvs = result.ProcessedEnvs;
            IList<string> changedFilePaths = result.ChangedFilePaths;
            IList<string> unchangedFilePaths = result.UnchangedFilePaths;

            foreach (ProcessedDecrypt processedEnv in processedEnvs)
            {
                _logger.Verbose($"decrypting {processedEnv.EnvFilePath} ({processedEnv.FilePath})");

                // Error and Changed are independent, not mutually exclusive: a per-key decryption
                // failure doesn't stop the file's other successfully-decrypted keys from being
                // written (matches decrypt.js exactly - confirmed live, this used to be an
                // `else if` that silently dropped partially-decrypted output on any error).
                if (processedEnv.Error != null)
                {
                    errorCount += 1;
                    Exception error = processedEnv.Error;
                    _logger.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
                }

                if (processedEnv.Changed)
                {
                    Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);

                    _logger.Verbose($"decrypted {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
                else
                {
                    _logger.Verbose($"no change {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
            }

            Dictionary<string, InjectSummaryFile> summaryRows = processedEnvs.GroupBy(row => row.EnvFilePath).ToDictionary(
                group => group.Key, group => new InjectSummaryFile(group.Last().EnvFilePath,
                    PublicKeys(group.Last().EnvSrc), group.Last().KeySources));
            InjectSummaryFile Row(string filepath)
            {
                return summaryRows.GetValueOrDefault(filepath) ?? new InjectSummaryFile(filepath);
            }

            if (changedFilePaths.Count > 0)
            {
                InjectSummarySources summary = InjectSummarySourcesOf(changedFilePaths.Select(Row));
                _logger.Success($"◇ decrypted {string.Join(',', summary.Files)}{summary.Suffix}");
            }
            else if (unchangedFilePaths.Count > 0)
            {
                InjectSummarySources summary = InjectSummarySourcesOf(unchangedFilePaths.Select(Row));
                _logger.Info($"○ no change to {string.Join(',', summary.Files)}{summary.Suffix}");
            }

            EventScope.Count(errorCount);

            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            EventScope.Fail(error);
            return 1;
        }
    }
}