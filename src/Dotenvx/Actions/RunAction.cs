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

    private readonly Option<string[]?> _ignoreOption =
        new("--ignore")
        {
            Description = "error code(s) to ignore (example: --ignore=MISSING_ENV_FILE)", HelpName = "errorCodes..."
        };

    private readonly IDotenvxLogger _logger;

    private readonly Option<bool> _opsOffOption =
        new("--ops-off") { Description = "disable dotenvx-ops features (default: false)" };

    private readonly Option<bool> _overloadOption =
        new("--overload", "-o")
        {
            Description =
                "override existing env variables (by default, existing env vars take precedence over .env files)"
        };

    private readonly Option<bool> _strictOption =
        new("--strict") { Description = "process.exit(1) on any errors (default: false)" };

    public RunAction(IDotenvxLogger logger) : base("run",
        "inject env at runtime [dotenvx run -- yourcommand]")
    {
        _logger = logger;
        // Options
        Add(_envOption);
        Add(_envFileOption);
        Add(_envKeysFileOption);
        Add(_overloadOption);
        Add(_strictOption);
        Add(_conventionOption);
        Add(_ignoreOption);
        Add(_opsOffOption);
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
        bool opsOff = parseResult.GetValue(_opsOffOption);

        // dotenvx-ops related
        bool opsOn = !opsOff;

        _logger.Debug($"process command [{string.Join(' ', commandArgs)}]");

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
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        // Check for separator '--'
        bool hasSeparator = parseResult.Tokens.Any(x => x.Type == TokenType.DoubleDash);

        if (commandArgs.Length < 1 || !hasSeparator)
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

        try
        {
            List<EnvItem> envItems = [];
            // handle shorthand conventions - like --convention=nextjs
            if (!string.IsNullOrEmpty(convention))
            {
                envItems.AddRange(Conventions(convention));
            }

            if (envs != null)
            {
                envItems.AddRange(envs.Select(e => new EnvItem { Type = EnvType.Env, Value = e }));
            }

            if (envFile != null)
            {
                envItems.AddRange(envFile.Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));
            }

            Run runService = Run.Factory(envItems.ToArray(), overload, processEnv, envKeysFile?.FirstOrDefault());
            RunResult result = runService.Execute();

            IList<RunProcessedEnv> processedEnvs = result.ProcessedEnvs;
            IList<string> readableStrings = result.ReadableStrings;
            IList<string> readableFilePaths = result.ReadableFilePaths;
            IList<string> uniqueInjectedKeys = result.UniqueInjectedKeys;

            if (opsOn)
            {
                try
                {
                    Ops.Factory(_logger).Observe(new OpsPayload
                    {
                        BeforeEnv = result.BeforeEnv,
                        ProcessedEnvs = result.ProcessedEnvs,
                        AfterEnv = result.AfterEnv
                    });
                }
                catch
                {
                    // ignore
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

                    if (code == "MISSING_ENV_FILE")
                    {
                        if (!string.IsNullOrEmpty(convention)) // do not output error for conventions (too noisy)
                        {
                            continue;
                        }

                        _logger.Error(error.Message);
                        if (!string.IsNullOrEmpty(dotenvError?.Help))
                        {
                            _logger.Error(
                                $"{dotenvError.Help} and re-run [dotenvx run -- {string.Join(' ', commandArgs)}]");
                        }
                    }
                    else
                    {
                        _logger.Error(error.Message);
                        if (!string.IsNullOrEmpty(dotenvError?.Help))
                        {
                            _logger.Error(dotenvError.Help);
                        }
                    }
                }

                // debug parsed
                _logger.Debug($"parsed: {JsonSerializer.Serialize(processedEnv.Parsed)}");

                // verbose/debug injected key/value
                foreach (KeyValuePair<string, string> kvp in processedEnv.Injected)
                {
                    _logger.Verbose($"{kvp.Key} set");
                    _logger.Debug($"{kvp.Key} set to {kvp.Value}");
                }

                // verbose/debug preExisted key/value
                foreach (KeyValuePair<string, string> kvp in processedEnv.PreExisted)
                {
                    _logger.Verbose($"{kvp.Key} pre-exists (protip: use --overload to override)");
                    _logger.Debug($"{kvp.Key} pre-exists as {kvp.Value} (protip: use --overload to override)");
                }
            }

            string msg = $"injecting env ({uniqueInjectedKeys.Count})";
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

            _logger.SuccessV(msg);
        }
        catch (Exception error)
        {
            _logger.Error(error.Message);
            if (error is DotenvxException dotenvxException && !string.IsNullOrEmpty(dotenvxException.Help))
            {
                _logger.Error(dotenvxException.Help);
            }

            return 1;
        }

        // Execute the command
        return await ExecuteCommand(_logger, commandArgs, processEnv);
    }
}