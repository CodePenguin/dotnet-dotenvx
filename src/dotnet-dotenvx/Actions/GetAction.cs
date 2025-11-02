using System;
using System.Collections.Generic;
using System.CommandLine;

using ParseResult = System.CommandLine.ParseResult;
using System.Linq;
using System.Text.Json;

using Dotenvx.Core.Interop.DotenvxJs.Helpers;
using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Shared;
using Dotenvx.Core.Interop.DotenvxJs.Types;

namespace dotnet_dotenvx.Actions;

public class GetAction : Command
{
    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };
    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true };
    
    private readonly Argument<string?> _keyArgument =
        new("key") { Arity = ArgumentArity.ZeroOrOne, Description = "environment variable name", HelpName = "key" };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "-f") { Description = "path(s) to your env file(s)" };

    private readonly Option<string[]?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file (default: same path as your env file"
        };

    private readonly Option<string[]?> _envOption =
        new("--env", "-e") { Description = "environment variable(s) set as string (example: \"HELLO=World\")" };

    private readonly Option<string[]?> _ignoreOption =
        new("--ignore")
        {
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)", HelpName = "errorCodes..."
        };

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly Option<bool> _strictOption =
        new("--strict") { Description = "Process exits on any errors" };

    private readonly Option<string> _conventionOption =
        new("--convention")
        {
            Description = "load a .env convention (available conventions: ['nextjs', 'flow'])", HelpName = "name"
        };

    private readonly Option<bool> _allOption =
        new("--all") { Description = "include all env files (even if they don't exist)" };

    private readonly Option<string> _formatOption =
        new("--format") { Description = "output format (eval, shell, json)" };

    private readonly Option<bool> _prettyPrintOption =
        new("--pretty-print") { Description = "pretty print json output" };

    private readonly IDotenvxLogger _logger;

    public GetAction(IDotenvxLogger logger) : base("get",
        "return the value of a specific environment variable")
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
        Add(_formatOption);
        Add(_prettyPrintOption);
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

        List<EnvItem> envItems = [];
        // handle shorthand conventions - like --convention=nextjs
        if (!string.IsNullOrEmpty(convention))
        {
            envItems.AddRange(Helpers.Conventions(convention));
        }

        if (envs != null)
        {
            envItems.AddRange(envs.Select(e => new EnvItem { Type = EnvType.Env, Value = e }));
        }

        if (envFile != null)
        {
            envItems.AddRange(envFile.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
        }

        try
        {
            Get getService = new(key, envItems, overload, all, envKeysFile?.FirstOrDefault());
            GetResult result = getService.Run();

            foreach (var error in result.Errors)
            {
                DotenvxException? dotenvError = error as DotenvxException;
                string code = dotenvError?.Code ?? "UNKNOWN";
                if (strict) throw error;

                if (ignoreList.Contains(code))
                {
                    continue;
                }

                _logger.Error(error.Message);
                if (!string.IsNullOrEmpty(dotenvError?.Help))
                {
                    _logger.Error(dotenvError.Help);
                }
            }

            if (key != null)
            {
                string? single = result.Parsed.TryGetValue(key, out var val) ? val : null;
                Console.WriteLine(single ?? "");
            }
            else
            {
                switch (format)
                {
                    case "eval":
                        {
                            string inline = string.Join('\n', result.Parsed.Select(kvp => $"{kvp.Key}={JsonSerializer.Serialize(kvp.Value)}"));
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
                            JsonSerializerOptions jsonOptions = prettyPrint ? _prettyPrintJsonOptions : _compactJsonOptions;
                            Console.WriteLine(JsonSerializer.Serialize(result.Parsed, jsonOptions));
                            break;
                        }
                }
            }
            return 0;
        }
        catch (Exception error)
        {
            _logger.Error(error.Message);
            if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
            {
                _logger.Error(ex.Help);
            }
            return 1;
        }
    }
}