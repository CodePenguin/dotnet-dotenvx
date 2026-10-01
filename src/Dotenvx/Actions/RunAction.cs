using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/run.js</remarks>
public class RunAction : Command
{
    private readonly Argument<string[]> _args =
        new("command") { Arity = ArgumentArity.ZeroOrMore, Hidden = true };

    private readonly Option<string> _conventionOption =
        new("--convention")
        {
            Description = "load a .env convention (available conventions: ['nextjs', 'flow'])", HelpName = "name"
        };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

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
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)", HelpName = "errorCodes...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly Option<bool> _redactOption =
        new("--redact") { Description = "redact injected values except keys ending in _PLAIN (default: false)" };

    private readonly Option<bool> _strictOption =
        new("--strict") { Description = "process.exit(1) on any errors (default: false)" };

    private readonly Option<bool> _validateOption =
        new("--validate") { Description = "validate against .env.example (default: false)" };

    private readonly Option<string?> _maskOption =
        new("--mask")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "inject masked values, optionally setting visible characters",
            HelpName = "characters"
        };

    public RunAction(IDotenvxLogger logger) : base("run",
        "inject env at runtime [dotenvx run -- yourcommand]")
    {
        _logger = logger;
        // Options
        Add(_envOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_redactOption);
        Add(_overloadOption);
        Add(_validateOption);
        Add(_strictOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        Add(_maskOption);
        Add(_args);
        SetAction(async pr => await Execute(pr));
    }

    private async Task<int> Execute(ParseResult parseResult)
    {
        string[]? envs = parseResult.GetValue(_envOption);
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        bool overload = parseResult.GetValue(_overloadOption);
        bool strict = parseResult.GetValue(_strictOption);
        string? convention = parseResult.GetValue(_conventionOption);
        string[] ignoreList = parseResult.GetValue(_ignoreOption) ?? [];
        string[] commandArgs = parseResult.GetValue(_args) ?? [];
        bool redactEnabled = parseResult.GetValue(_redactOption);
        bool validateEnabled = parseResult.GetValue(_validateOption);

        System.CommandLine.Parsing.OptionResult? maskResult = parseResult.GetResult(_maskOption);
        bool maskEnabled = maskResult != null;
        object showChar = 6;
        if (maskEnabled)
        {
            showChar = parseResult.GetValue(_maskOption) ?? (object)6;
        }

        ConfigOptions normalizedOptions =
            NormalizeDotenvConfigIgnore(NormalizeDotenvConfigConvention(new ConfigOptions
            {
                Convention = convention, Ignore = ignoreList.ToList()
            }));
        convention = normalizedOptions.Convention;
        ignoreList = normalizedOptions.Ignore?.ToArray() ?? [];

        _logger.Debug($"process command [{string.Join(' ', commandArgs)}]");

        var options = new
        {
            envs = maskEnabled ? envs?.Select(e => MaskEnvSrc(e, showChar)).ToArray() : envs,
            envFile,
            envKeysFile,
            overload,
            strict,
            convention,
            ignoreList
        };
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        // Check for separator '--'
        bool hasSeparator = parseResult.Tokens.Any(x => x.Type == TokenType.DoubleDash);

        if (commandArgs.Length < 1)
        {
            if (hasSeparator)
            {
                _logger.Error("missing command after [dotenvx run --]. try [dotenvx run -- yourcommand]");
            }
            else
            {
                string realExample = envFile?.FirstOrDefault() ?? ".env";
                _logger.Error(
                    $"ambiguous command due to missing '--' separator. try [dotenvx run -f {realExample} -- yourcommand]");
            }

            return 1;
        }

        IDictionary<string, string> processEnv = EnvironmentHelper.GetEnvironmentVariablesAsDictionary();
        IDictionary<string, string> commandEnv = processEnv;
        string[] sensitiveValues = [];

        try
        {
            List<EnvItem> explicitEnvItems =
                EnvItemOrdering.BuildExplicitEnvItemsInOrder(parseResult, _envOption, _envFileOption);

            EnvItem[] envItems = BuildCommandEnvs(NormalizeDotenvConfigPath(explicitEnvItems.ToArray()), convention);

            Run runService = Run.Factory(envItems, overload, processEnv, envKeysFile?.FirstOrDefault());
            RunResult result = runService.Execute();

            IList<RunProcessedEnv> processedEnvs = result.ProcessedEnvs;
            IList<string> readableStrings = result.ReadableStrings;
            IList<string> readableFilePaths = result.ReadableFilePaths;
            IList<string> uniqueInjectedKeys = result.UniqueInjectedKeys;

            if (redactEnabled)
            {
                sensitiveValues = RedactedValues(processedEnvs);
            }

            if (maskEnabled)
            {
                commandEnv = new Dictionary<string, string>(processEnv);
                foreach (RunProcessedEnv processedEnv in processedEnvs)
                {
                    foreach (string key in processedEnv.Parsed.Keys)
                    {
                        if (commandEnv.TryGetValue(key, out string? existingValue))
                        {
                            commandEnv[key] = Mask(existingValue, showChar);
                        }
                    }
                }
            }

            if (validateEnabled)
            {
                DotenvxException? validationError = ValidateEnvExample(result.AfterEnv);
                if (validationError != null)
                {
                    if (ignoreList.Contains(validationError.Code))
                    {
                        _logger.Verbose($"ignored: {validationError.Message}");
                    }
                    else if (strict)
                    {
                        throw validationError;
                    }
                    else
                    {
                        _logger.Error(validationError.MessageWithHelp ?? validationError.Message);
                    }
                }
            }

            foreach (RunProcessedEnv processedEnv in processedEnvs)
            {
                switch (processedEnv.Type)
                {
                    case EnvType.EnvFile:
                        _logger.Verbose(
                            $"loading env from {processedEnv.FilePath} ({Path.GetFullPath(processedEnv.FilePath)})");
                        break;
                    case EnvType.Env:
                        _logger.Verbose($"loading env from string ({processedEnv.String})");
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                foreach (Exception error in processedEnv.Errors)
                {
                    DotenvxException? dotenvError = error as DotenvxException;
                    string code = dotenvError?.Code ?? "UNKNOWN";
                    if (ignoreList.Contains(code))
                    {
                        _logger.Verbose($"ignored: {error.Message}");
                        continue; // ignore error
                    }

                    if (parseResult.GetValue(_strictOption))
                    {
                        throw error; // throw if strict and not ignored
                    }

                    if (code == "MISSING_ENV_FILE" && !string.IsNullOrEmpty(convention))
                    {
                        // do not output error for conventions (too noisy)
                    }
                    else
                    {
                        _logger.Error(dotenvError?.MessageWithHelp ?? error.Message);
                    }
                }

                IReadOnlyDictionary<string, string> parsedForDisplay = processedEnv.Parsed;
                IReadOnlyDictionary<string, string> injectedForDisplay = processedEnv.Injected;
                IReadOnlyDictionary<string, string> preExistedForDisplay = processedEnv.PreExisted;
                if (maskEnabled)
                {
                    parsedForDisplay = MaskDict(processedEnv.Parsed, showChar);
                    injectedForDisplay = MaskDict(processedEnv.Injected, showChar);
                    preExistedForDisplay = MaskDict(processedEnv.PreExisted, showChar);
                }

                // debug parsed
                Dictionary<string, string> debugParsed = redactEnabled
                    ? RedactOutputDict(parsedForDisplay, sensitiveValues)
                    : parsedForDisplay.ToDictionary();
                _logger.Debug($"parsed: {JsonSerializer.Serialize(debugParsed)}");

                // verbose/debug injected key/value
                foreach (KeyValuePair<string, string> kvp in injectedForDisplay)
                {
                    _logger.Verbose($"{kvp.Key} set");
                    _logger.Debug(RedactOutputText($"{kvp.Key} set to {kvp.Value}", sensitiveValues)!);
                }

                // verbose/debug preExisted key/value
                foreach (KeyValuePair<string, string> kvp in preExistedForDisplay)
                {
                    _logger.Verbose($"{kvp.Key} pre-exists (protip: use --overload to override)");
                    _logger.Debug(RedactOutputText(
                        $"{kvp.Key} pre-exists as {kvp.Value} (protip: use --overload to override)", sensitiveValues)!);
                }
            }

            string msg = $"injected env ({uniqueInjectedKeys.Count})";
            switch (readableFilePaths.Count)
            {
                case > 0 when readableStrings.Count > 0:
                    msg +=
                        $" from {string.Join(", ", readableFilePaths)}, and --env flag{(readableStrings.Count > 1 ? "s" : "")}";
                    break;
                case > 0:
                    msg += $" from {string.Join(", ", readableFilePaths)}";
                    break;
                default:
                    {
                        if (readableStrings.Count > 0)
                        {
                            msg += $" from --env flag{(readableStrings.Count > 1 ? "s" : "")}";
                        }

                        break;
                    }
            }

            _logger.Success($"⟐ {msg}");
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            return 1;
        }

        // Execute the command
        return await ExecuteCommand(_logger, commandArgs, commandEnv, sensitiveValues);
    }

    private static Dictionary<string, string> MaskDict(IReadOnlyDictionary<string, string> value, object showChar)
    {
        return value.ToDictionary(kvp => kvp.Key, kvp => Mask(kvp.Value, showChar));
    }
}