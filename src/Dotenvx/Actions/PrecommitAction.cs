using System;
using System.CommandLine;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/precommit.js</remarks>
public class PrecommitAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory")
        {
            Arity = ArgumentArity.ZeroOrOne, Description = "directory to prevent committing .env files from (default: \".\")"
        };

    private readonly Option<bool> _installOption =
        new("--install", "-i") { Description = "install to .git/hooks/pre-commit" };

    private readonly IDotenvxLogger _logger;

    public PrecommitAction(IDotenvxLogger logger) : base("precommit", "prevent committing .env files to code")
    {
        _logger = logger;
        Add(_directoryArgument);
        Add(_installOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string directory = parseResult.GetValue(_directoryArgument) ?? ".";
        bool install = parseResult.GetValue(_installOption);

        _logger.Debug($"directory: {directory}");
        _logger.Debug($"options: {JsonSerializer.Serialize(new { install })}");

        try
        {
            Precommit precommitService = Precommit.Factory(directory, install);
            PrecommitResult result = precommitService.Run();

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
