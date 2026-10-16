using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Events;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/del.js</remarks>
public class DelAction : Command
{
    private readonly Option<string[]?> _envFileOption =
        new("--file", "-f", "--env-file") { Description = "path(s) to your env file(s) (default: [])", HelpName = "paths..." };

    private readonly Argument<string> _keyArgument =
        new("key") { Description = "KEY", HelpName = "KEY" };

    private readonly IDotenvxLogger _logger;

    public DelAction(IDotenvxLogger logger) : base("del",
        "delete a single environment variable")
    {
        _logger = logger;
        Add(_keyArgument);
        Add(_envFileOption);
        SetAction(Execute);
        EventTracking.Track(this, "del");
    }

    private int Execute(ParseResult parseResult)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "deleting");

        string key = parseResult.GetValue(_keyArgument)!;
        string[]? envFile = parseResult.GetValue(_envFileOption);

        _logger.Debug($"key: {key}");

        _logger.Debug($"options: {JsonStringify(Help.CommanderOpts.Build(parseResult))}");

        try
        {
            List<EnvItem> envItems = [];
            envItems.AddRange(
                SplitCommaSeparatedEnvFiles(envFile).Select(f => new EnvItem { Type = EnvType.EnvFile, Value = f }));

            Del delService = Del.Factory(key, envItems.ToArray());
            DelResult result = delService.Run();

            int errorCount = 0;
            foreach (ProcessedDel processedEnv in result.ProcessedEnvs)
            {
                _logger.Verbose($"deleting for {processedEnv.EnvFilePath}");

                if (processedEnv.Error != null)
                {
                    errorCount++;
                    _logger.Error((processedEnv.Error as DotenvxException)?.MessageWithHelp ?? processedEnv.Error.Message);
                }
                else if (processedEnv.Changed)
                {
                    Fsx.WriteFileX(processedEnv.FilePath, processedEnv.EnvSrc);
                    _logger.Verbose($"{processedEnv.Key} deleted ({processedEnv.EnvFilePath})");
                }
                else
                {
                    _logger.Verbose($"no change {processedEnv.EnvFilePath} ({processedEnv.FilePath})");
                }
            }

            if (result.ChangedFilePaths.Length > 0)
            {
                _logger.Success($"◇ removed {key} ({string.Join(',', result.ChangedFilePaths)})");
            }
            else if (result.UnchangedFilePaths.Length > 0)
            {
                _logger.Info($"○ no change ({string.Join(',', result.UnchangedFilePaths)})");
            }

            EventScope.Count(errorCount);

            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            EventScope.Fail(error);
            return 1;
        }
    }
}
