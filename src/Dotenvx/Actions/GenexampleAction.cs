using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/genexample.js</remarks>
public class GenexampleAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory") { Arity = ArgumentArity.ZeroOrOne, Description = "directory to generate from (default: \".\")" };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f")
        {
            Description = "path(s) to your env file(s) (default: \".env\")", HelpName = "paths..."
        };

    private readonly IDotenvxLogger _logger;

    public GenexampleAction(IDotenvxLogger logger) : base("genexample", "generate .env.example")
    {
        _logger = logger;
        Add(_directoryArgument);
        Add(_envFileOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string directory = parseResult.GetValue(_directoryArgument) ?? ".";
        string[]? rawEnvFile = parseResult.GetValue(_envFileOption);
        string[]? envFile = rawEnvFile is { Length: > 0 } ? rawEnvFile : null;

        _logger.Debug($"directory: {directory}");
        _logger.Debug($"options: {JsonSerializer.Serialize(new { envFile = envFile ?? [".env"] })}");

        try
        {
            Genexample genexampleService = Genexample.Factory(directory, envFile ?? [".env"]);
            GenexampleResult result = genexampleService.Run();

            _logger.Verbose($"loading env from {string.Join(',', result.EnvFile)}");
            Fsx.WriteFileX(result.ExampleFilepath, result.EnvExampleFile);

            if (result.AddedKeys.Length > 0)
            {
                _logger.Success($"▣ generated ({Path.GetFileName(result.ExampleFilepath)})");
            }
            else
            {
                _logger.Info("○ no change (.env.example)");
            }

            return 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            return 1;
        }
    }
}
