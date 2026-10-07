using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using DotenvxJsHelpers = Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

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
    private readonly Option<string[]?> _envFileOption =
        new("--env-file", "-f") { Description = "path(s) to your env file(s)", HelpName = "paths..." };

    private readonly Option<string[]?> _envKeysFileOption =
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

    private readonly Option<bool> _noArmorOption = SecretResolverOptions.NoArmor();

    private readonly Option<bool> _noNativeOption = SecretResolverOptions.NoNative();

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
        Add(_noNativeOption);
        Add(_noArmorOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "retrieving");

        // JS `if (key)`: an empty key means "all"
        string? key = parseResult.GetValue(_keyArgument) is { Length: > 0 } keyValue ? keyValue : null;
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        string? formatValue = parseResult.GetValue(_formatOption);
        string format = formatValue ?? "json";
        bool prettyPrint = parseResult.GetValue(_prettyPrintOption);

        if (key != null)
        {
            _logger.Debug($"key: {key}");
        }

        var options = new { envFile, envKeysFile, format, prettyPrint };
        _logger.Debug($"options: {JsonStringify(options)}");

        try
        {
            string[] split = DotenvxJsHelpers.SplitCommaSeparatedEnvFiles(envFile);
            string[] resolvedEnvFiles = // -f given but blank (`-f " "`) means no files at all, not the .env default
            DotenvxJsHelpers.Filepaths(split.Length == 0 && (envFile is null || envFile.Length == 0) ? null : split);
            Dictionary<string, string?> results = [];
            foreach (string resolvedEnvFile in resolvedEnvFiles)
            {
                if (!Fsx.ExistsSync(resolvedEnvFile))
                {
                    throw new Exception(
                        $"ENOENT: no such file or directory, open '{Path.GetFullPath(resolvedEnvFile)}'");
                }

                KeyPair keyPair = KeyPair.Factory(resolvedEnvFile, envKeysFile is { Length: > 0 } ? envKeysFile : null);
                keyPair.KeyAccess = SecretResolverOptions.KeyAccess(_logger, parseResult.GetValue(_noNativeOption), parseResult.GetValue(_noArmorOption), null, SecretResolverOptions.ArmorMode.FlagOnly,
                spinner.OnStatus);
                foreach ((string resultKey, string? resultValue) in keyPair.Run())
                {
                    results[resultKey] = resultValue;
                }
            }

            if (key != null)
            {
                // keypair.js's check is `results === undefined` (key entirely absent from the
                // resolved object), not falsy - a key that WAS derived but has a null value (e.g.
                // DOTENV_PUBLIC_KEY on a plaintext-only file) still falls through to printing it,
                // and JS's template-literal/console.log coercion renders that null as the literal
                // text "null". `GetValueOrDefault` can't distinguish "absent" from "present but
                // null" the way `TryGetValue`'s bool return can - using the wrong one here made a
                // present-but-null key take the "not found" branch (empty line, exit 1) instead of
                // printing "null" and exiting 0, confirmed live against Node.
                if (!results.TryGetValue(key, out string? single))
                {
                    Spinners.StopActive();
                    Console.WriteLine("");
                    return 1;
                }

                string singleText = single ?? "null";
                if (format == "colon")
                {
                    Spinners.StopActive();
                    Console.WriteLine($"{key}:{singleText}");
                }
                else
                {
                    Spinners.StopActive();
                    Console.WriteLine(singleText);
                }
            }
            else
            {
                if (format == "shell")
                {
                    string inline = string.Join(' ', results.Select(kvp => $"{kvp.Key}={kvp.Value ?? ""}"));
                    Spinners.StopActive();
                    Console.WriteLine(inline);
                }
                else if (format == "colon")
                {
                    string inline = string.Join(' ', results.Select(kvp => $"{kvp.Key}:{kvp.Value ?? ""}"));
                    Spinners.StopActive();
                    Console.WriteLine(inline);
                }
                else
                {
                    Spinners.StopActive();
                    Console.WriteLine(JsonStringify(results, prettyPrint));
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