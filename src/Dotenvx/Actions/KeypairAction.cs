using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using DotenvxJsHelpers = Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/keypair.js. Fixed 2026-09-25, two independent bugs:
/// (1) no try/catch at all around keyPair.Run() - keypair.js wraps its whole body in
/// try/catch -&gt; catchAndLog(error) -&gt; exit 1, so any resolver error was previously an unhandled
/// .NET exception instead of a clean single-line stderr message + exit 1; (2) a missing env file
/// silently returned null keys with exit 0 instead of Node's raw, untranslated ENOENT error - the
/// real resolver calls `fsx.readFileX(filepath)` with no existence check, letting the raw fs error
/// propagate (fsx.js's readFileX does zero translation, unlike run/get/decrypt's own higher-layer
/// resolver code). The existence check belongs HERE, not inside KeyPair.cs itself - that class is
/// also used internally by FindPrivateKey.cs/FindPublicKey.cs as an "does a key already exist yet"
/// lookup for run/encrypt/set's fresh-key-generation logic, which legitimately needs the graceful
/// null-return behavior for files that don't exist yet (confirmed by breaking `encrypt` on a fresh
/// file when this check was first added to KeyPair.cs itself instead).
/// </remarks>
public class KeyPairAction : Command
{
    private readonly JsonSerializerOptions _compactJsonOptions = new() { WriteIndented = false };

    private readonly Option<string?> _envFileOption =
        new("--file", "--env-file", "-f") { Description = "path(s) to your env file(s)", HelpName = "paths..." };

    private readonly Option<string?> _envKeysFileOption =
        new("--env-keys-file", "-fk")
        {
            Description = "path(s) to your .env.keys file(s) (default: same path as your env file)",
            HelpName = "path"
        };

    private readonly Option<string> _formatOption =
        new("--format")
        {
            Description = "format of the output (json, shell, colon) (default: \"json\")", HelpName = "type"
        };

    private readonly Argument<string?> _keyArgument =
        new("key") { Arity = ArgumentArity.ZeroOrOne, Description = "environment variable key name", HelpName = "KEY" };

    private readonly IDotenvxLogger _logger;

    // NewLine = "\n" matches Node's JSON.stringify (always "\n"); WriteIndented alone defaults to
    // Environment.NewLine ("\r\n" on Windows).
    private readonly JsonSerializerOptions _prettyPrintJsonOptions = new() { WriteIndented = true, NewLine = "\n" };

    private readonly Option<bool> _prettyPrintOption =
        new("--pretty-print", "-pp", "--pp") { Description = "pretty print output" };

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

        try
        {
            string resolvedEnvFile = envFile ?? ".env";
            if (!Fsx.ExistsSync(resolvedEnvFile))
            {
                throw new Exception($"ENOENT: no such file or directory, open '{Path.GetFullPath(resolvedEnvFile)}'");
            }

            KeyPair keyPair = KeyPair.Factory(resolvedEnvFile, envKeysFile);
            Dictionary<string, string?> results = keyPair.Run();

            if (key != null)
            {
                string? single = results.GetValueOrDefault(key);
                if (single == null)
                {
                    Console.WriteLine("");
                    return 1;
                }

                if (format == "colon")
                {
                    Console.WriteLine($"{key}:{single}");
                }
                else
                {
                    Console.WriteLine(single);
                }
            }
            else
            {
                if (format == "shell")
                {
                    string inline = string.Join(' ', results.Select(kvp => $"{kvp.Key}={kvp.Value ?? ""}"));
                    Console.WriteLine(inline);
                }
                else if (format == "colon")
                {
                    string inline = string.Join(' ', results.Select(kvp => $"{kvp.Key}:{kvp.Value ?? ""}"));
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
        catch (Exception error)
        {
            DotenvxJsHelpers.CatchAndLog(_logger, error);
            return 1;
        }
    }
}