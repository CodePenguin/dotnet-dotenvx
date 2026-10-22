using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Custodians;
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
        new("--file", "-f", "--env-file") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: same path as your env file)",
            HelpName = "path"
        };

    private readonly Option<string[]?> _excludeKeyOption =
        new("--exclude-key", "-ek")
        {
            Description = "keys(s) to exclude from encryption (default: none)",
            HelpName = "excludeKeys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string[]?> _keyOption =
        new("--key", "-k")
        {
            Description = "keys(s) to encrypt (default: all keys in file)",
            HelpName = "keys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _noArmorOption = SecretResolverOptions.NoArmor();
    private readonly Option<string?> _tokenOption = SecretResolverOptions.Token();

    private readonly Option<bool> _noNativeOption = SecretResolverOptions.NoNative();
    private readonly Option<bool> _no1PasswordOption = SecretResolverOptions.No1Password("disable 1Password features");
    private readonly Option<bool> _noBitwardenOption = SecretResolverOptions.NoBitwarden("disable Bitwarden features");

    private readonly Option<string?> _withOption = new("--with")
    {
        Description = "encrypt with this public key; accepts a fresh file or matching existing public key",
        HelpName = "public-key"
    };

    private readonly Option<string?> _profileOption = new("--profile") { Description = "select an Envfile profile", HelpName = "name" };

    private readonly Option<bool> _noCreateOption =
        new("--no-create") { Description = "do not create .env file(s) when missing" };

    private readonly Option<bool> _stdoutOption =
        new("--stdout") { Description = "send to stdout" };

    public EncryptAction(IDotenvxLogger logger) : base("encrypt",
        "encrypt .env file(s)")
    {
        Aliases.Add("enc"); // shorthand
        _logger = logger;
        Add(_withOption);
        Add(_profileOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_keyOption);
        Add(_excludeKeyOption);
        Add(_stdoutOption);
        Add(_noCreateOption);
        Add(_no1PasswordOption);
        Add(_noBitwardenOption);
        Add(_noNativeOption);
        Add(_noArmorOption);
        Add(_tokenOption);
        SetAction(Execute);
        EventTracking.Track(this, "encrypt");
    }

    private KeyAccessOptions KeyAccess(ParseResult parseResult, Action<string>? onStatus = null)
    {
        // encrypt.js: noArmor = options.armor === false (the custody menu decides about Armor, not the session)
        KeyAccessOptions access = SecretResolverOptions.KeyAccess(_logger, parseResult.GetValue(_noNativeOption),
            parseResult.GetValue(_noArmorOption), parseResult.GetValue(_tokenOption),
            SecretResolverOptions.ArmorMode.FlagOnly, onStatus);
        access.No1Password = parseResult.GetValue(_no1PasswordOption);
        access.NoBitwarden = parseResult.GetValue(_noBitwardenOption);
        access.Interactive = true;
        NativeCustodian.Logger = _logger;

        return access;
    }

    private int Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "encrypting");

        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        string[]? key = parseResult.GetValue(_keyOption);
        string[]? excludeKey = parseResult.GetValue(_excludeKeyOption);
        string? profile = parseResult.GetValue(_profileOption);
        string? with = parseResult.GetValue(_withOption);
        bool stdout = parseResult.GetValue(_stdoutOption);
        bool noCreate = parseResult.GetValue(_noCreateOption);

        _logger.Debug($"options: {JsonStringify(Help.CommanderOpts.Build(parseResult))}");

        List<EnvItem> envItems = [];
        envItems.AddRange(
            SplitCommaSeparatedEnvFiles(envFile).Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));

        string? envKeysFilePath = envKeysFile?.LastOrDefault(); // plain `<path>` option: commander keeps the LAST occurrence
        string fk = envKeysFilePath ?? ".env.keys";

        // stdout - should not have a try so that exit codes can surface to stdout
        if (stdout)
        {
            Encrypt encryptService = new([.. envItems], key, excludeKey, envKeysFilePath, noCreate)
            {
                KeyAccess = KeyAccess(parseResult),
                Profile = profile,
                With = with
            };
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
                    Spinners.StopActive();
                    Console.WriteLine(processedEnv.EnvSrc);
                }
            }

            EventScope.Count(stdoutErrorCount);

            return stdoutErrorCount > 0 ? 1 : 0; // exit early
        }

        try
        {
            KeyAccessOptions access = KeyAccess(parseResult, spinner.OnStatus);
            Encrypt encryptService = new([.. envItems], key, excludeKey, envKeysFilePath, noCreate)
            {
                KeyAccess = access,
                Profile = profile,
                With = with
            };
            EncryptResult result = encryptService.Run();

            if (!string.IsNullOrEmpty(result.KeysSrc) && with is null)
            {
                Fsx.WriteKeyFile(fk, result.KeysSrc);
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

            // Existing public keys can encrypt without private-key access. Metadata lookup must never turn successful
            // encryption into a failure.
            foreach (EncryptProcessedEnv row in processedEnvs)
            {
                if (with is not null || row.Error != null || row.KeySources != null || PublicKeys(row.EnvSrc).Length == 0)
                {
                    continue;
                }

                try
                {
                    KeyPair keyPair = KeyPair.Factory(row.FilePath, [fk]);
                    keyPair.KeyAccess = access;
                    keyPair.Run();
                    row.KeySources = new Dictionary<string, string?>
                    {
                        [PublicKeys(row.EnvSrc)[0]] = keyPair.Origin
                    }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!);
                }
                catch (Exception)
                {
                    // ignored
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
                _logger.Success($"◈ encrypted {string.Join(',', summary.Files)}{summary.Suffix}");
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
