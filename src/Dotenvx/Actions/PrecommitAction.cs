using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
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
        new("--install", "-i") { Description = "install a pre-commit hook" };

    private readonly Option<bool> _uninstallOption = new("--uninstall") { Description = "remove the dotenvx pre-commit hook" };

    private readonly IDotenvxLogger _logger;

    public PrecommitAction(IDotenvxLogger logger) : base("precommit", "deprecated. use [dotenvx protect]")
    {
        _logger = logger;
        Add(_directoryArgument);
        Add(_installOption);
        Add(_uninstallOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string directory = parseResult.GetValue(_directoryArgument) ?? ".";
        bool install = parseResult.GetValue(_installOption);
        bool uninstall = parseResult.GetValue(_uninstallOption);

        _logger.Debug($"directory: {directory}");
        Dictionary<string, bool> options = [];
        if (install)
        {
            options["install"] = true;
        }

        if (uninstall)
        {
            options["uninstall"] = true;
        }

        _logger.Debug($"options: {JsonStringify(options)}");

        try
        {
            if (uninstall)
            {
                if (install)
                {
                    throw new Exception("--install and --uninstall cannot be used together");
                }

                Protect.UninstallResult removal = Protect.UninstallPrecommitHook(directory);
                if (removal.Warning is not null)
                {
                    _logger.Warn(removal.Warning);
                }
                else
                {
                    _logger.Success(removal.Removed
                        ? "▣ uninstalled (dotenvx precommit hook)"
                        : "○ no dotenvx precommit hook found");
                }

                return 0;
            }

            _logger.Warn("[DEPRECATED] dotenvx precommit. fix: run [dotenvx protect]");
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
