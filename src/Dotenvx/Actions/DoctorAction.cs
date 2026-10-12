using System.Linq;
using System.CommandLine;

using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/doctor.js (registered hidden in dotenvx.js)</remarks>
public class DoctorAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory")
        {
            Arity = ArgumentArity.ZeroOrOne, Description = "directory to scan"
        };

    private readonly IDotenvxLogger _logger;

    public DoctorAction(IDotenvxLogger logger) : base("doctor", "scan for dotenv loaders")
    {
        _logger = logger;
        Add(_directoryArgument);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string directory = parseResult.GetValue(_directoryArgument) ?? ".";
        _logger.Debug($"directory: {directory}");

        DoctorFinding[] findings = Doctor.Factory(directory).Run();
        _logger.Debug($"findings: {JsonStringify(findings.Select(f => new { lang = f.Lang, filepath = f.Filepath, line = f.Line, code = f.Code, msg = f.Msg }))}");

        if (findings.Length == 0)
        {
            _logger.Info("no dotenv loaders found");
            return 0;
        }

        _logger.Warn($"found {findings.Length} possible dotenv loader{(findings.Length == 1 ? "" : "s")}");

        foreach (DoctorFinding finding in findings)
        {
            _logger.Info($"│ {finding.Filepath}:{finding.Line}: {finding.Code}");
        }

        return 0;
    }
}
