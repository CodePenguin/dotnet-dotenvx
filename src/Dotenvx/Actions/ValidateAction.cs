using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/validate.js. Unlike run, validate does NOT special-
/// case MISSING_ENV_FILE-with-convention as "too noisy to show" - confirmed by reading validate.js's
/// error loop, which has no such branch. Also unlike run.js/get.js, validate.js builds its env list
/// via plain `buildCommandEnvs(this.envs, options.convention)` - NOT wrapped in
/// normalizeDotenvConfigPath first - so, deliberately reproduced here, `validate` does not fall
/// back to DOTENV_FILE/DOTENV_PATH/DOTENV_F when no -f/--env is given, while run/get do. Confirmed
/// by direct comparison of all three action files at this pin, 2026-09-24 - not "fixed" toward
/// consistency, since the project's standing convention is to faithfully reproduce upstream's own
/// asymmetries rather than smooth them over.
/// </remarks>
public class ValidateAction : Command
{
    private readonly Option<string> _conventionOption =
        new("--convention")
        {
            Description = "load a .env convention (available conventions: ['nextjs', 'flow'])", HelpName = "name"
        };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f")
        {
            Description = "path(s) to your env file(s) (default: [])", HelpName = "path"
        };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path(s) to your .env.keys file(s) (default: same path as your env file)", HelpName = "path"
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

    public ValidateAction(IDotenvxLogger logger) : base("validate", "validate .env file(s) against .env.example")
    {
        _logger = logger;
        Add(_envOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_overloadOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string[]? envs = parseResult.GetValue(_envOption);
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        bool overload = parseResult.GetValue(_overloadOption);
        string? convention = parseResult.GetValue(_conventionOption);
        string[] ignoreList = parseResult.GetValue(_ignoreOption) ?? [];

        ConfigOptions normalizedOptions =
            NormalizeDotenvConfigIgnore(NormalizeDotenvConfigConvention(new ConfigOptions
            {
                Convention = convention, Ignore = ignoreList.ToList()
            }));
        convention = normalizedOptions.Convention;
        ignoreList = normalizedOptions.Ignore?.ToArray() ?? [];

        _logger.Debug(
            $"options: {JsonStringify(new { envs, envFile, envKeysFile, overload, convention, ignoreList })}");

        // Isolated copy, never mutating the real process environment - matches validate.js's own
        // `const validateEnv = { ...process.env }`.
        IDictionary<string, string> validateEnv = EnvironmentHelper.GetEnvironmentVariablesAsDictionary();

        try
        {
            List<EnvItem> explicitEnvItems =
                EnvItemOrdering.BuildExplicitEnvItemsInOrder(parseResult, _envOption, _envFileOption);

            EnvItem[] envItems = BuildCommandEnvs(explicitEnvItems.ToArray(), convention);

            Run runService = Run.Factory(envItems, overload, validateEnv, envKeysFile?.FirstOrDefault());
            RunResult result = runService.Execute();

            int errorCount = 0;
            foreach (RunProcessedEnv processedEnv in result.ProcessedEnvs)
            {
                foreach (Exception error in processedEnv.Errors)
                {
                    DotenvxException? dotenvError = error as DotenvxException;
                    string code = dotenvError?.Code ?? "";
                    if (ignoreList.Contains(code))
                    {
                        _logger.Verbose($"ignored: {error.Message}");
                        continue;
                    }

                    errorCount++;
                    _logger.Error(dotenvError?.MessageWithHelp ?? error.Message);
                }
            }

            DotenvxException? validationError = ValidateEnvExample(result.AfterEnv);
            if (validationError != null)
            {
                if (ignoreList.Contains(validationError.Code))
                {
                    _logger.Verbose($"ignored: {validationError.Message}");
                }
                else
                {
                    errorCount++;
                    _logger.Error(validationError.MessageWithHelp);
                }
            }

            if (errorCount > 0)
            {
                return 1;
            }

            _logger.Success("▣ validated");
            return 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            return 1;
        }
    }
}
