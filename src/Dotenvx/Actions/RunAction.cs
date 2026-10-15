using Dotenvx.Interop.DotenvxJs.Helpers;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Events;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/run.js</remarks>
public class RunAction : Command
{
    private readonly Argument<string[]> _args =
        new("command") { Arity = ArgumentArity.ZeroOrMore, Hidden = true };

    private readonly Option<string?> _profileOption = new("--profile") { Description = "select an Envfile profile", HelpName = "name" };

    private readonly Option<string> _conventionOption =
        new("--convention")
        {
            Description = "load a .env convention (available conventions: ['nextjs', 'flow'])", HelpName = "name"
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
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)", HelpName = "errorCodes...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _noArmorOption = SecretResolverOptions.NoArmor();
    private readonly Option<string?> _tokenOption = SecretResolverOptions.Token();

    private readonly Option<bool> _noNativeOption = SecretResolverOptions.NoNative();

    private readonly Option<bool> _no1PasswordOption = SecretResolverOptions.No1Password();
    private readonly Option<bool> _noBitwardenOption = SecretResolverOptions.NoBitwarden();

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly Option<bool> _redactOption =
        new("--redact") { Description = "redact injected values except public keys (Envfile rules take precedence) (default: false)" };

    private readonly Option<bool> _strictOption =
        new("--strict") { Description = "process.exit(1) on any errors (default: false)" };


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
        Add(_strictOption);
        Add(_profileOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        Add(_maskOption);
        Add(_no1PasswordOption);
        Add(_noBitwardenOption);
        Add(_noNativeOption);
        Add(_noArmorOption);
        Add(_tokenOption);
        Add(_args);
        SetAction(async pr => await Execute(pr));
        EventTracking.Track(this, "run");
    }

    /// <remarks>Reference source: dotenvx/src/cli/actions/run.js's inferCommandArgsFromProcessArgv (`process.argv` here is the program arguments).</remarks>
    private static string[] InferCommandArgsFromProcessArgv(string[] argv)
    {
        int runIndex = Array.IndexOf(argv, "run");
        if (runIndex == -1)
        {
            return [];
        }

        string[] args = argv[(runIndex + 1)..];
        int separatorIndex = Array.IndexOf(args, "--");
        if (separatorIndex != -1)
        {
            return args[(separatorIndex + 1)..];
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "-f" or "--file" or "--env-file" or "--profile")
            {
                i++;

                continue;
            }

            if (args[i].StartsWith('-'))
            {
                continue;
            }

            return args[i..];
        }

        return [];
    }

    private async Task<int> Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "injecting");

        string[]? envs = parseResult.GetValue(_envOption);
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        bool overload = parseResult.GetValue(_overloadOption);
        bool strict = parseResult.GetValue(_strictOption);
        string? profile = parseResult.GetValue(_profileOption);
        string? convention = parseResult.GetValue(_conventionOption);
        string[] ignoreList = parseResult.GetValue(_ignoreOption) ?? [];
        string[] commandArgs = parseResult.GetValue(_args) ?? [];
        string[] processArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (commandArgs.Length < 1)
        {
            commandArgs = InferCommandArgsFromProcessArgv(processArgs);
        }
        bool redactEnabled = parseResult.GetValue(_redactOption);

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

        System.Text.Json.Nodes.JsonObject options = RunDebugOptions(parseResult, maskEnabled, showChar);

        // Check for separator '--'
        bool hasSeparator = processArgs.Contains("--") || parseResult.Tokens.Any(x => x.Type == TokenType.DoubleDash);

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

            KeyAccessOptions keyAccess = SecretResolverOptions.KeyAccess(_logger, parseResult.GetValue(_noNativeOption),
                parseResult.GetValue(_noArmorOption), parseResult.GetValue(_tokenOption),
                SecretResolverOptions.ArmorMode.SessionUnlessToken, spinner.OnStatus);
            keyAccess.Interactive = true;
            keyAccess.Command = string.Join(' ', commandArgs);
            keyAccess.No1Password = parseResult.GetValue(_no1PasswordOption);
            keyAccess.NoBitwarden = parseResult.GetValue(_noBitwardenOption);

            ValidatedEnv validated = PrepareValidatedEnv.Execute([.. explicitEnvItems], new ValidateEnvOptions
            {
                Profile = profile,
                Convention = convention,
                Overload = overload,
                EnvKeysFile = envKeysFile,
                KeyAccess = keyAccess,
                No1Password = keyAccess.No1Password,
                NoBitwarden = keyAccess.NoBitwarden,
                PromptForSecrets = true
            }, processEnv, requireEnvfile: false);

            IList<RunProcessedEnv> processedEnvs = validated.ProcessedEnvs;
            if (validated.HasEnvfile || redactEnabled)
            {
                sensitiveValues = RedactedValues(processedEnvs, validated.Schema, processEnv, redactEnabled);
            }

            _logger.Debug(RedactOutputText($"options: {JsonStringify(options)}", sensitiveValues)!);
            _logger.Debug(RedactOutputText($"process command [{string.Join(' ', commandArgs)}]", sensitiveValues)!);

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

            foreach (RunProcessedEnv processedEnv in processedEnvs)
            {
                switch (processedEnv.Type)
                {
                    case EnvType.EnvFile:
                        _logger.Verbose(
                            $"loading env from {processedEnv.FilePath} ({NodePath.Resolve(processedEnv.FilePath)})");
                        break;
                    case EnvType.Env:
                        _logger.Verbose(RedactOutputText(
                            $"loading env from string ({(maskEnabled ? MaskEnvSrc(processedEnv.String, showChar) : processedEnv.String)})",
                            sensitiveValues)!);
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                foreach (Exception error in processedEnv.Errors)
                {
                    string code = ErrorCodeOf(error) ?? "UNKNOWN";
                    if (ignoreList.Contains(code))
                    {
                        _logger.Verbose($"ignored: {error.Message}");
                        continue; // ignore error
                    }

                    if (strict)
                    {
                        throw error; // file-loading failures use the CLI strict flag
                    }

                    if (code == "MISSING_ENV_FILE" && !string.IsNullOrEmpty(convention))
                    {
                        // do not output error for conventions (too noisy)
                    }
                    else
                    {
                        _logger.Error((error as DotenvxException)?.MessageWithHelp ?? error.Message);
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
                // a file that could not be loaded has no `parsed` (JS prints `undefined`)
                _logger.Debug(processedEnv.Errors.Any(failure => ErrorCodeOf(failure) == "MISSING_ENV_FILE")
                    ? "undefined"
                    : JsonStringify(RedactOutputDict(parsedForDisplay, sensitiveValues)));

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

            InvalidEnvException? validationError = validated.ValidationError;
            if (validationError is not null)
            {
                if (ignoreList.Contains(validationError.Code))
                {
                    _logger.Verbose($"ignored: {validationError.Message}");
                }
                else if (strict || validationError.Diagnostics.Any(diagnostic => diagnostic.Strict))
                {
                    throw validationError;
                }
                else
                {
                    _logger.Warn(validationError.MessageWithHelp ?? validationError.Message);
                }
            }

            int injectedCount = processedEnvs.SelectMany(row => row.Injected.Keys).Distinct().Count();
            _logger.Success(Primitives.Primitives.InjectSummary(injectedCount, validated.ReadableFilePaths.Select(filepath =>
            {
                RunProcessedEnv? row = processedEnvs.FirstOrDefault(item => item.FilePath == filepath);

                return new InjectSummaryFile(filepath, row is null ? [] : Primitives.Primitives.PublicKeys(row.Src),
                    row?.KeySources);
            })));
        }
        catch (PromptCancelledException cancelled)
        {
            EventScope.Fail(cancelled);
            return 130;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            EventScope.Fail(error);
            return 1;
        }

        // Execute the command
        return await ExecuteCommand(_logger, commandArgs, commandEnv, sensitiveValues);
    }

    /// <summary>
    /// run.js: `normalizeDotenvConfigIgnore(normalizeDotenvConfigConvention(normalizeDotenvConfigQuiet(this.opts())))`, then the
    /// `--mask` view of it that the debug line prints.
    /// </summary>
    private static System.Text.Json.Nodes.JsonObject RunDebugOptions(ParseResult parseResult, bool maskEnabled, object showChar)
    {
        System.Text.Json.Nodes.JsonObject options = Help.CommanderOpts.Normalize(Help.CommanderOpts.Build(parseResult), true, true);

        if (maskEnabled)
        {
            var masked = new System.Text.Json.Nodes.JsonArray();
            if (options["env"] is System.Text.Json.Nodes.JsonArray envs)
            {
                foreach (System.Text.Json.Nodes.JsonNode? item in envs)
                {
                    masked.Add(MaskEnvSrc(item!.GetValue<string>(), showChar));
                }
            }

            options["env"] = masked;
            if (options["token"] is { } token)
            {
                options["token"] = Mask(token.GetValue<string>(), showChar);
            }
        }

        return options;
    }

    private static Dictionary<string, string> MaskDict(IReadOnlyDictionary<string, string> value, object showChar)
    {
        return value.ToDictionary(kvp => kvp.Key, kvp => Mask(kvp.Value, showChar));
    }
}
