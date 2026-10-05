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
using static Dotenvx.Interop.PicomatchJs.IndexJs;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/get.js</remarks>
public class GetAction : Command
{
    private readonly Option<bool> _no1PasswordOption = SecretResolverOptions.No1Password();
    private readonly Option<bool> _noBitwardenOption = SecretResolverOptions.NoBitwarden();

    private readonly Option<bool> _allOption =
        new("--all", "-a") { Description = "include all machine envs as well" };

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

    private readonly Option<string[]?> _excludeKeyOption =
        new("--exclude-key", "-ek")
        {
            Description = "keys(s) to exclude from output (default: none)", HelpName = "excludeKeys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string> _formatOption =
        new("--format")
        {
            Description = "format of the output (json, shell, colon, eval, eval-export) (default: \"json\")",
            HelpName = "type"
        };

    private readonly Option<string[]?> _ignoreOption =
        new("--ignore")
        {
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)", HelpName = "errorCodes...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Option<string[]?> _includeKeyOption =
        new("--include-key", "-ik")
        {
            Description = "keys(s) to include in output (default: all)", HelpName = "includeKeys...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
        };

    private readonly Argument<string?> _keyArgument =
        new("key") { Arity = ArgumentArity.ZeroOrOne, Description = "environment variable name", HelpName = "KEY" };

    private readonly IDotenvxLogger _logger;

    private readonly Option<string?> _maskOption =
        new("--mask")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "mask values, optionally setting visible characters",
            HelpName = "characters"
        };

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    // dotenvx.js registers `--pp` as a SEPARATE commander option (`.option('--pp', 'pretty print
    // output (alias)')`) alongside `-pp, --pretty-print` - the same shape already correctly ported
    // for keypair (KeyPairAction.cs), but missing here: `get --pp` fell through to the `[KEY]`
    // positional and failed with MISSING_KEY instead of pretty-printing, confirmed live.
    private readonly Option<bool> _prettyPrintOption =
        new("--pretty-print", "-pp", "--pp") { Description = "pretty print output" };

    private readonly Option<bool> _strictOption =
        new("--strict")
        {
            Description = "process.exit(1) on any errors (default: false)", DefaultValueFactory = _ => false
        };

    public GetAction(IDotenvxLogger logger) : base("get",
        "return a single environment variable")
    {
        _logger = logger;
        Add(_keyArgument);
        Add(_envOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_overloadOption);
        Add(_strictOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        Add(_allOption);
        Add(_includeKeyOption);
        Add(_excludeKeyOption);
        Add(_maskOption);
        Add(_prettyPrintOption);
        Add(_formatOption);
        Add(_no1PasswordOption);
        Add(_noBitwardenOption);
        SetAction(Execute);
    }

    private static IReadOnlyDictionary<string, string> FilterKeys(
        IReadOnlyDictionary<string, string> parsed, string[]? includeKeys, string[]? excludeKeys)
    {
        Func<string, bool> include = Picomatch(includeKeys);
        Func<string, bool> exclude = Picomatch(excludeKeys);

        return parsed.Where(kvp =>
                !(includeKeys?.Length > 0 && !include(kvp.Key)) &&
                !(excludeKeys?.Length > 0 && exclude(kvp.Key)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private int Execute(ParseResult parseResult)
    {
        string? key = parseResult.GetValue(_keyArgument);
        string[]? envs = parseResult.GetValue(_envOption);
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        bool overload = parseResult.GetValue(_overloadOption);
        bool strict = parseResult.GetValue(_strictOption);
        string? convention = parseResult.GetValue(_conventionOption);
        string[] ignoreList = parseResult.GetValue(_ignoreOption) ?? [];
        string[]? includeKey = parseResult.GetValue(_includeKeyOption);
        string[]? excludeKey = parseResult.GetValue(_excludeKeyOption);

        ConfigOptions normalizedOptions =
            NormalizeDotenvConfigIgnore(NormalizeDotenvConfigConvention(new ConfigOptions
            {
                Convention = convention, Ignore = ignoreList.ToList()
            }));
        convention = normalizedOptions.Convention;
        ignoreList = normalizedOptions.Ignore?.ToArray() ?? [];

        bool all = parseResult.GetValue(_allOption);
        string? formatValue = parseResult.GetValue(_formatOption);
        string format = formatValue ?? "json";
        bool prettyPrint = parseResult.GetValue(_prettyPrintOption);

        if (key != null)
        {
            _logger.Debug($"key: {key}");
        }

        var options = new
        {
            envs,
            envFile,
            envKeysFile,
            overload,
            strict,
            convention,
            ignoreList,
            all,
            format,
            prettyPrint
        };
        _logger.Debug($"options: {JsonStringify(options)}");

        List<EnvItem> explicitEnvItems =
            EnvItemOrdering.BuildExplicitEnvItemsInOrder(parseResult, _envOption, _envFileOption);

        EnvItem[] envItems = BuildCommandEnvs(NormalizeDotenvConfigPath(explicitEnvItems.ToArray()), convention);

        try
        {
            Get getService = Get.Factory(key, envItems, overload, all, ResolveEnvKeysFile(envKeysFile));
            getService.No1Password = parseResult.GetValue(_no1PasswordOption);
            getService.NoBitwarden = parseResult.GetValue(_noBitwardenOption);
            getService.PromptForSecrets = true;
            GetResult result = getService.Run();
            IReadOnlyDictionary<string, string> parsed = FilterKeys(result.Parsed, includeKey, excludeKey);

            System.CommandLine.Parsing.OptionResult? maskResult = parseResult.GetResult(_maskOption);
            if (maskResult != null)
            {
                object? showChar = parseResult.GetValue(_maskOption);
                if (showChar is null)
                {
                    showChar = 6;
                }

                parsed = parsed.ToDictionary(kvp => kvp.Key, kvp => Mask(kvp.Value, showChar));
            }

            int errorCount = 0;
            foreach (Exception error in result.Errors)
            {
                DotenvxException? dotenvError = error as DotenvxException;
                string code = dotenvError?.Code ?? "UNKNOWN";
                if (strict)
                {
                    throw error;
                }

                if (ignoreList.Contains(code))
                {
                    continue;
                }

                errorCount++;
                _logger.Error(dotenvError?.MessageWithHelp ?? error.Message);
            }

            if (key != null)
            {
                string? single = parsed.TryGetValue(key, out string? val) ? val : null;
                Console.WriteLine(single ?? "");
            }
            else
            {
                switch (format)
                {
                    case "eval":
                    case "eval-export":
                        {
                            string prefix = format == "eval-export" ? "export " : "";
                            string inline = string.Join('\n',
                                parsed.Select(kvp => $"{prefix}{kvp.Key}={Escape(kvp.Value)}"));
                            Console.WriteLine(inline);
                            break;
                        }
                    case "shell":
                        {
                            string inline = string.Join(' ', parsed.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                            Console.WriteLine(inline);
                            break;
                        }
                    case "colon":
                        {
                            string inline = string.Join(' ', parsed.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                            Console.WriteLine(inline);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine(JsonStringify(parsed, prettyPrint));
                            break;
                        }
                }
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