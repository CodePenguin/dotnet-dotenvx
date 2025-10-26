using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dotenvx.Core.Interop.DotenvxJs.Helpers;
using Dotenvx.Core.Interop.DotenvxJs.Services;
using Dotenvx.Core.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Logging;

using ParseResult = System.CommandLine.ParseResult;

namespace dotnet_dotenvx.Actions;

public class RunAction : Command
{
    private readonly Option<string> _conventionOption =
        new("--convention") { Description = "load a .env convention (available conventions: ['nextjs', 'flow'])" };

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
        new("--ignore") { Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)" };

    private readonly ILogger<RunAction> _logger;

    private readonly Option<bool> _opsOffOption =
        new("--ops-off") { Description = "disable dotenvx-ops features", Hidden = true };

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly Option<bool> _strictOption =
        new("--strict") { Description = "Process exits on any errors" };

    public RunAction(ILogger<RunAction> logger) : base("Run",
        "inject env at runtime [dotnet dotenvx run -- yourcommand]")
    {
        _logger = logger;
        TreatUnmatchedTokensAsErrors = false;
        Add(_envOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_overloadOption);
        Add(_strictOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        Add(_opsOffOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string[]? envs = parseResult.GetValue(_envOption);
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? envKeysFile = parseResult.GetValue(_envKeysFileOption);
        bool overload = parseResult.GetValue(_overloadOption);
        bool strict = parseResult.GetValue(_strictOption);
        string? convention = parseResult.GetValue(_conventionOption);
        string[] ignoreList = parseResult.GetValue(_ignoreOption) ?? [];
        string[] commandArgs = parseResult.UnmatchedTokens.ToArray();
        bool opsOff = parseResult.GetValue(_opsOffOption);

        // dotenvx-ops related
        bool opsOnFlag = !opsOff;

        _logger.LogDebug("process command [{Join}]", string.Join(' ', commandArgs));

        var options = new
        {
            envs,
            envFile,
            overload,
            strict,
            convention,
            ignoreList,
            opsOff
        };
        _logger.LogDebug("options: {Options}", JsonSerializer.Serialize(options));

        if (commandArgs.Length < 1)
        {
            // Check for separator '--'
            bool hasSeparator = Environment.GetCommandLineArgs().Contains("--");

            if (hasSeparator)
            {
                _logger.LogError("missing command after [dotenvx run --]. try [dotenvx run -- yourcommand]");
            }
            else
            {
                string realExample = envFile?.FirstOrDefault() ?? ".env";
                _logger.LogError(
                    "ambiguous command due to missing '--' separator. try [dotenvx run -f {RealExample} -- yourcommand]",
                    realExample);
            }

            return 1;
        }

        try
        {
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

            string? dotenvKey = Environment.GetEnvironmentVariable("DOTENV_KEY");
            IDictionary processEnv = Environment.GetEnvironmentVariables();

            Run runService = new(envItems, overload, dotenvKey,
                (IDictionary<string, string>)processEnv, envKeysFile?.FirstOrDefault(), opsOnFlag);
            RunResult result = runService.Execute();

            IList<ProcessedEnv> processedEnvs = result.ProcessedEnvs;
            IList<string> readableStrings = result.ReadableStrings;
            IList<string> readableFilepaths = result.ReadableFilePaths;
            IList<string> uniqueInjectedKeys = result.UniqueInjectedKeys;

            // TODO: Implement Radar and Ops services

            foreach (ProcessedEnv processedEnv in processedEnvs)
            {
                switch (processedEnv.Type)
                {
                    case EnvType.EnvFile when !string.IsNullOrEmpty(processedEnv.PrivateKey):
                        _logger.LogInformation(
                            "loading env from encrypted {ProcessedEnvFilePath} ({FullFilePath})",
                            processedEnv.FilePath, Path.GetFullPath(processedEnv.FilePath));
                        _logger.LogDebug(
                            "decrypting encrypted env from {ProcessedEnvFilePath} ({FullFilePath})",
                            processedEnv.FilePath, Path.GetFullPath(processedEnv.FilePath));
                        break;
                    case EnvType.EnvFile:
                        _logger.LogInformation(
                            "loading env from {ProcessedEnvFilePath} ({FullFilePath})",
                            processedEnv.FilePath, Path.GetFullPath(processedEnv.FilePath));
                        break;
                    case EnvType.Env:
                        _logger.LogInformation(
                            "loading env from string ({ProcessedEnv})",
                            processedEnv.String);
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
                        _logger.LogInformation("ignored: {ErrorMessage}", error.Message);
                        continue; // ignore error
                    }

                    if (parseResult.GetValue(_strictOption))
                    {
                        throw error; // throw if strict and not ignored
                    }

                    if (code == "MISSING_ENV_FILE")
                    {
                        if (!string.IsNullOrEmpty(convention)) // do not output error for conventions (too noisy)
                        {
                            continue;
                        }

                        _logger.LogError(error.Message);
                        if (!string.IsNullOrEmpty(dotenvError?.Help))
                        {
                            _logger.LogError(
                                "{ErrorHelp} and re-run [dotenvx run -- {Command}]",
                                dotenvError.Help, string.Join(' ', commandArgs));
                        }
                    }
                    else
                    {
                        _logger.LogError("{ErrorMessage}", error.Message);
                        if (!string.IsNullOrEmpty(dotenvError?.Help))
                        {
                            _logger.LogError("{ErrorHelp}", dotenvError.Help);
                        }
                    }
                }

                // debug parsed
                _logger.LogDebug("parsed: {Serialize}", JsonSerializer.Serialize(processedEnv.Parsed));

                // verbose/debug injected key/value
                foreach (KeyValuePair<string, string> kvp in processedEnv.Injected)
                {
                    _logger.LogInformation("{KvpKey} set", kvp.Key);
                    _logger.LogDebug("{KvpKey} set to {KvpValue}", kvp.Key, kvp.Value);
                }

                // verbose/debug preExisted key/value
                foreach (KeyValuePair<string, string> kvp in processedEnv.PreExisted)
                {
                    _logger.LogInformation("{KvpKey} pre-exists (protip: use --overload to override)", kvp.Key);
                    _logger.LogDebug("{KvpKey} pre-exists as {KvpValue} (protip: use --overload to override)", kvp.Key,
                        kvp.Value);
                }
            }

            string msg = $"injecting env ({uniqueInjectedKeys.Count})";
            switch (readableFilepaths.Count)
            {
                case > 0 when readableStrings.Count > 0:
                    msg +=
                        $" from {string.Join(", ", readableFilepaths)}, and --env flag{(readableStrings.Count > 1 ? "s" : "")}";
                    break;
                case > 0:
                    msg += $" from {string.Join(", ", readableFilepaths)}";
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

            _logger.LogInformation("{Message}", msg);
        }
        catch (Exception error)
        {
            _logger.LogError("{ErrorMessage}", error.Message);
            if (error is DotenvxException ex && !string.IsNullOrEmpty(ex.Help))
            {
                _logger.LogError("{ErrorHelp}", ex.Help);
            }

            return 1;
        }

        // Execute the command
        return ExecuteCommand(commandArgs, Environment.GetEnvironmentVariables());
    }

    private static int ExecuteCommand(string[] commandArgs, IDictionary environmentVariables)
    {
        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = commandArgs[0],
                Arguments = string.Join(' ', commandArgs.Skip(1)),
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            }
        };

        // Set environment variables
        foreach (DictionaryEntry entry in environmentVariables)
        {
            process.StartInfo.EnvironmentVariables[entry.Key.ToString()!] = entry.Value?.ToString();
        }

        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }
}