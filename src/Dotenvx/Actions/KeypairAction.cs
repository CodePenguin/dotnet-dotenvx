using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/keypair.js</remarks>
public class KeyPairAction : Command
{
    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };

    private readonly Option<string?> _envFileOption =
        new("--env-file", "-f") { Description = "path(s) to your env file(s)", HelpName = "paths..." };

    private readonly Option<string?> _envKeysFileOption =
        new("--env-keys-file", "-fk") { Description = "path to your .env.keys file  (default: [])", HelpName = "path" };

    private readonly Option<string> _formatOption =
        new("--format") { Description = "output format (json, shell) (default: \"json\")", HelpName = "type" };

    private readonly Argument<string?> _keyArgument =
        new("key") { Arity = ArgumentArity.ZeroOrOne, Description = "environment variable key name", HelpName = "KEY" };

    private readonly IDotenvxLogger _logger;

    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true };

    private readonly Option<bool> _prettyPrintOption =
        new("--pretty-print") { Description = "pretty print output" };

    public KeyPairAction(IDotenvxLogger logger) : base("keypair",
        "print public/private keys for .env file(s)")
    {
        _logger = logger;
        Add(_keyArgument);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_formatOption);
        Add(_prettyPrintOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string? key = parseResult.GetValue(_keyArgument);
        string? envFile = parseResult.GetValue(_envFileOption);
        string? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        string? formatValue = parseResult.GetValue(_formatOption);
        string format = formatValue ?? "json";
        bool prettyPrint = parseResult.GetValue(_prettyPrintOption);

        if (key != null)
        {
            _logger.Debug($"key: {key}");
        }

        var options = new { envFile, envKeysFile, format, prettyPrint };
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        KeyPair keyPair = KeyPair.Factory(envFile ?? ".env", envKeysFile);
        Dictionary<string, string?> results = keyPair.Run();

        if (key != null)
        {
            string? single = results.GetValueOrDefault(key);
            if (single == null)
            {
                Console.WriteLine("");
                return 1;
            }

            Console.WriteLine(single);
        }
        else
        {
            if (format == "shell")
            {
                string inline = string.Join(' ', results.Select(kvp => $"{kvp.Key}={kvp.Value ?? ""}"));
                Console.WriteLine(inline);
            }
            else
            {
                JsonSerializerOptions jsonOptions =
                    prettyPrint ? _prettyPrintJsonOptions : _compactJsonOptions;
                Console.WriteLine(JsonSerializer.Serialize(results, jsonOptions));
            }
        }

        return 0;
    }
}