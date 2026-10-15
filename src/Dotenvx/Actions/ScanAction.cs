using System.CommandLine;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/ext/scan.js. Ext-only - confirmed no top-level
/// `dotenvx scan` exists upstream, don't add one. No service class either (confirmed absent
/// upstream) - this action shells out to the external `gitleaks` binary directly via
/// GitleaksHelper's swappable seam, since there's no reusable "service" logic beyond invoking it
/// and formatting output.
/// </remarks>
public class ScanAction : Command
{
    private readonly IDotenvxLogger _logger;

    public ScanAction(IDotenvxLogger logger) : base("scan", "scan for leaked secrets")
    {
        _logger = logger;
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        _logger.Debug($"options: {JsonStringify(Help.CommanderOpts.Build(parseResult))}");

        (int versionExitCode, _) = GitleaksHelper.RunCommand("version");
        if (versionExitCode != 0)
        {
            _logger.Error("gitleaks: command not found");
            _logger.Help("fix: install gitleaks:      [brew install gitleaks]");
            _logger.Help("fix: other install options: [https://github.com/gitleaks/gitleaks]");
            return 1;
        }

        (int detectExitCode, string output) = GitleaksHelper.RunCommand("detect --no-banner --verbose");
        if (detectExitCode == 0)
        {
            _logger.Info(Chomp(output));
            return 0;
        }

        // `error.stdout` is a Buffer, which is truthy even when empty: a failure with no output still logs an empty error
        _logger.Error(Chomp(output));

        return 1;
    }
}
