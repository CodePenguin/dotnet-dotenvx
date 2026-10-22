using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Logging;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/check.js (hidden `check`: Envfile rules against env file(s)).</remarks>
public class CheckAction : Command
{
    private readonly Option<string?> _conventionOption =
        new("--convention")
        {
            Description = "load a .env convention (available conventions: ['nextjs', 'flow'])",
            HelpName = "name"
        };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "-f", "--env-file") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path(s) to your .env.keys file(s) (default: same path as your env file)",
            HelpName = "path"
        };

    private readonly Option<string[]?> _envOption =
        new("--env", "-e")
        {
            Description = "environment variable(s) set as string (example: \"HELLO=World\") (default: [])",
            HelpName = "strings...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string[]?> _ignoreOption =
        new("--ignore")
        {
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)",
            HelpName = "errorCodes...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly IDotenvxLogger _logger;
    private readonly DotenvxLoggerOptions _loggerOptions;
    private readonly Option<bool> _no1PasswordOption = SecretResolverOptions.No1Password();
    private readonly Option<bool> _noArmorOption = SecretResolverOptions.NoArmor();
    private readonly Option<bool> _noBitwardenOption = SecretResolverOptions.NoBitwarden();
    private readonly Option<bool> _noNativeOption = SecretResolverOptions.NoNative();

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly Option<string?> _profileOption = new("--profile") { Description = "select an Envfile profile", HelpName = "name" };
    private readonly Option<string?> _tokenOption = SecretResolverOptions.Token();

    public CheckAction(IDotenvxLogger logger, DotenvxLoggerOptions loggerOptions) : base("check",
        "check Envfile rules against .env file(s)")
    {
        _logger = logger;
        _loggerOptions = loggerOptions;
        Hidden = true;
        Add(_envOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_overloadOption);
        Add(_profileOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        Add(_tokenOption);
        Add(_noArmorOption);
        Add(_noNativeOption);
        Add(_no1PasswordOption);
        Add(_noBitwardenOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "checking");

        string? convention = parseResult.GetValue(_conventionOption);
        string[] ignore = parseResult.GetValue(_ignoreOption) ?? [];
        ConfigOptions normalized = NormalizeDotenvConfigIgnore(NormalizeDotenvConfigConvention(new ConfigOptions
        {
            Convention = convention,
            Ignore = [.. ignore]
        }));
        convention = normalized.Convention;
        ignore = normalized.Ignore?.ToArray() ?? [];

        bool showPreview = _loggerOptions.LogLevel >= DotenvxLogLevel.Info;
        int errorCount = 0;
        List<string> checkedSources = [];
        List<(string Level, string Message)> messages = [];

        void Report(string level, string message)
        {
            messages.Add((level, message));
        }

        void Flush()
        {
            spinner.Dispose();
            foreach ((string level, string message) in messages)
            {
                switch (level)
                {
                    case "infoerror":
                        _logger.InfoError(message);
                        break;
                    default:
                        _logger.Error(message);
                        break;
                }
            }

            messages.Clear();
        }

        try
        {
            List<EnvItem> explicitEnvItems =
                EnvItemOrdering.BuildExplicitEnvItemsInOrder(parseResult, _envOption, _envFileOption);
            KeyAccessOptions keyAccess = SecretResolverOptions.KeyAccess(_logger, parseResult.GetValue(_noNativeOption),
                parseResult.GetValue(_noArmorOption), parseResult.GetValue(_tokenOption),
                SecretResolverOptions.ArmorMode.SessionUnlessToken, spinner.OnStatus);
            keyAccess.Interactive = true;
            keyAccess.No1Password = parseResult.GetValue(_no1PasswordOption);
            keyAccess.NoBitwarden = parseResult.GetValue(_noBitwardenOption);

            ValidatedEnv validated = PrepareValidatedEnv.Execute([.. explicitEnvItems], new ValidateEnvOptions
            {
                Profile = parseResult.GetValue(_profileOption),
                Convention = convention,
                Overload = parseResult.GetValue(_overloadOption),
                EnvKeysFile = parseResult.GetValue(_envKeysFileOption),
                KeyAccess = keyAccess,
                No1Password = keyAccess.No1Password,
                NoBitwarden = keyAccess.NoBitwarden
            });

            checkedSources.AddRange(validated.ReadableFilePaths);
            if (validated.ProcessedEnvs.Any(row => row.Type == EnvType.Env && row.Parsed.Count > 0))
            {
                checkedSources.Add("--env");
            }

            if (checkedSources.Count == 0 && validated.Schema.Keys.Keys.Any(validated.Checked.ContainsKey))
            {
                checkedSources.Add("shell environment");
            }

            Func<string, string> location = EnvfileDiagnostics.Locations(validated.ProcessedEnvs,
                checkedSources.Count == 0 ? "Envfile" : string.Join(", ", checkedSources));
            foreach (RunProcessedEnv row in validated.ProcessedEnvs)
            {
                foreach (Exception error in row.Errors)
                {
                    string? code = ErrorCodeOf(error);
                    if (code is not null && ignore.Contains(code))
                    {
                        continue;
                    }

                    if (code == "MISSING_ENV_FILE" && !validated.RequiredFilePaths.Contains(NodePath.Resolve(row.FilePath)))
                    {
                        Report("infoerror", $"○ skipped ({row.FilePath})");
                        continue;
                    }

                    errorCount++;
                    Report("error", (error as DotenvxException)?.MessageWithHelp ?? error.Message);
                }
            }

            InvalidEnvException? validationError = validated.ValidationError;
            bool validationActive = validationError is not null && !ignore.Contains(validationError.Code);
            if (validationActive)
            {
                errorCount++;
                if (!showPreview)
                {
                    foreach (var diagnostic in validationError!.Diagnostics)
                    {
                        string message = diagnostic.Message.Replace($"{diagnostic.Key} is ", $"{diagnostic.Key} ",
                            StringComparison.Ordinal);
                        Report("error", $"{message} ({location(diagnostic.Key)})");
                    }
                }
            }

            spinner.Dispose();
            if (showPreview)
            {
                string preview = EnvfileDiagnostics.Preview(validated.ProcessedEnvs, validated.Schema, validated.Checked,
                    validationActive ? validationError!.Diagnostics : []);
                if (preview != "")
                {
                    foreach (string line in preview.Split('\n'))
                    {
                        _logger.Info(line);
                    }
                }
            }

            Flush();
            if (checkedSources.Count == 0)
            {
                _logger.Error("no environment sources found to check");

                return 1;
            }

            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception error)
        {
            Flush();
            CatchAndLog(_logger, error);

            return 1;
        }
    }
}
