using System;
using System.CommandLine;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/prebuild.js</remarks>
public class PrebuildAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory")
        {
            Arity = ArgumentArity.ZeroOrOne, Description = "directory to prevent including .env files from (default: \".\")"
        };

    private readonly IDotenvxLogger _logger;

    public PrebuildAction(IDotenvxLogger logger) : base("prebuild", "deprecated. use [dotenvx protect --docker]")
    {
        _logger = logger;
        Add(_directoryArgument);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        _logger.Warn("[DEPRECATED] dotenvx prebuild. fix: run [dotenvx protect --docker]");
        string directory = parseResult.GetValue(_directoryArgument) ?? ".";

        _logger.Debug($"directory: {directory}");
        _logger.Debug($"options: {JsonStringify(Help.CommanderOpts.Build(parseResult))}");

        try
        {
            Prebuild prebuildService = Prebuild.Factory(directory);
            PrebuildResult result = prebuildService.Run();

            foreach (CustomException warning in result.Warnings)
            {
                _logger.Warn(warning.MessageWithHelp);
            }

            _logger.Success(result.SuccessMessage);

            return 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            return 1;
        }
    }
}
