using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;

using Dotenvx.Helpers;
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
            Description = "path(s) to your env file(s) (default: \".env\")", HelpName = "paths...",
            AllowMultipleArgumentsPerToken = true,
            CustomParser = OptionParsers.ParseMultiValue
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
        // genexample.js has no own default handling - it relies on commander's own scalar default
        // ('.env', a bare string, not an array) for `-f, --env-file <paths...>`, so `options.envFile`
        // stays a plain string when `-f` is never passed at all; only an EXPLICIT `-f` produces an
        // array. Confirmed live: `--debug` with no `-f` prints `{"envFile":".env"}` on Node, not
        // `{"envFile":[".env"]}` - matched here even though every other code path treats both shapes
        // identically (a 1-element array and its scalar behave the same everywhere else).
        object debugEnvFile = envFile ?? (object)".env";
        _logger.Debug($"options: {JsonStringify(new { envFile = debugEnvFile })}");

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
