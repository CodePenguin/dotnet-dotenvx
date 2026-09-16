using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/get.js</remarks>
public class GetAction : Command
{
    private readonly Option<bool> _allOption =
        new("--all", "-a") { Description = "include all machine envs as well" };

    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };

    private readonly Option<string> _conventionOption =
        new("--convention")
        {
            Description = "load a .env convention (available conventions: ['nextjs', 'flow'])", HelpName = "name"
        };

    private readonly Option<string[]?> _envFileOption =
        new("--env-file", "-f") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: same path as your env file)", HelpName = "path"
        };

    private readonly Option<string[]?> _envOption =
        new("--env", "-e")
        {
            Description = "environment variable(s) set as string (example: \"HELLO=World\") (default: [])",
            HelpName = "strings..."
        };

    private readonly Option<string> _formatOption =
        new("--format")
        {
            Description = "format of the output (json, shell, eval) (default: \"json\")", HelpName = "type"
        };

    private readonly Option<string[]?> _ignoreOption =
        new("--ignore")
        {
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)", HelpName = "errorCodes..."
        };

    private readonly Argument<string?> _keyArgument =
        new("key") { Arity = ArgumentArity.ZeroOrOne, Description = "environment variable name", HelpName = "KEY" };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true };

    private readonly Option<bool> _prettyPrintOption =
        new("--pretty-print", "-pp") { Description = "pretty print output" };

    private readonly Option<bool> _strictOption =
        new("--strict") { Description = "process.exit(1) on any errors", DefaultValueFactory = _ => false };

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
        Add(_prettyPrintOption);
        Add(_formatOption);
        SetAction(Execute);
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
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        List<EnvItem> explicitEnvItems = [];
        if (envs != null)
        {
            explicitEnvItems.AddRange(envs.Select(e => new EnvItem { Type = EnvType.Env, Value = e }));
        }

        if (envFile != null)
        {
            explicitEnvItems.AddRange(envFile.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
        }

        List<EnvItem> envItems = [];
        // handle shorthand conventions - like --convention=nextjs
        if (!string.IsNullOrEmpty(convention))
        {
            envItems.AddRange(Conventions(convention));
        }

        envItems.AddRange(NormalizeDotenvConfigPath(explicitEnvItems.ToArray()));

        try
        {
            Get getService = Get.Factory(key, envItems.ToArray(), overload, all, envKeysFile?.FirstOrDefault());
            GetResult result = getService.Run();

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
                _logger.Error(error.Message);
                if (!string.IsNullOrEmpty(dotenvError?.Help))
                {
                    _logger.Error(dotenvError.Help);
                }
            }

            if (key != null)
            {
                string? single = result.Parsed.TryGetValue(key, out string? val) ? val : null;
                Console.WriteLine(single ?? "");
            }
            else
            {
                switch (format)
                {
                    case "eval":
                        {
                            string inline = string.Join('\n',
                                result.Parsed.Select(kvp => $"{kvp.Key}={JsonSerializer.Serialize(kvp.Value)}"));
                            Console.WriteLine(inline);
                            break;
                        }
                    case "shell":
                        {
                            string inline = string.Join(' ', result.Parsed.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                            Console.WriteLine(inline);
                            break;
                        }
                    default:
                        {
                            JsonSerializerOptions jsonOptions =
                                prettyPrint ? _prettyPrintJsonOptions : _compactJsonOptions;
                            Console.WriteLine(JsonSerializer.Serialize(result.Parsed, jsonOptions));
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