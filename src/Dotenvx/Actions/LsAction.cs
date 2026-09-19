using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/ls.js</remarks>
public class LsAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory")
        {
            Arity = ArgumentArity.ZeroOrOne, Description = "directory to list .env files from (default: \".\")"
        };

    private readonly Option<string[]?> _envFileOption =
        new("--file", "--env-file", "-f")
        {
            Description = "path(s) to your env file(s) (default: \".env*\")", HelpName = "filenames..."
        };

    private readonly Option<string[]?> _excludeEnvFileOption =
        new("--exclude-env-file", "-ef")
        {
            Description = "path(s) to exclude from your env file(s) (default: none)",
            HelpName = "excludeFilenames..."
        };

    private readonly Option<bool> _jsonOption =
        new("--json") { Description = "output a JSON array of absolute filepaths" };

    private readonly IDotenvxLogger _logger;

    public LsAction(IDotenvxLogger logger) : base("ls",
        "print all .env files in a tree structure")
    {
        _logger = logger;
        Add(_directoryArgument);
        Add(_envFileOption);
        Add(_excludeEnvFileOption);
        Add(_jsonOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string directory = parseResult.GetValue(_directoryArgument) ?? "./";
        string[]? envFile = parseResult.GetValue(_envFileOption);
        string[]? excludeEnvFile = parseResult.GetValue(_excludeEnvFileOption);
        bool json = parseResult.GetValue(_jsonOption);

        // debug args
        _logger.Debug($"directory: {directory}");

        var options = new { envFile, excludeEnvFile };
        _logger.Debug($"options: {JsonSerializer.Serialize(options)}");

        Ls lsService = Ls.Factory(directory, envFile, excludeEnvFile);
        string[] filepaths = lsService.Run();
        _logger.Debug($"filepaths: {JsonSerializer.Serialize(filepaths)}");

        if (json)
        {
            string cwd = Path.GetFullPath(directory);
            string[] absoluteFilepaths = filepaths.Select(filepath => Path.GetFullPath(Path.Combine(cwd, filepath)))
                .ToArray();
            // JsonSerializerOptions.WriteIndented defaults to Environment.NewLine ("\r\n" on
            // Windows) for indentation breaks; Node's JSON.stringify always emits "\n" - explicit
            // NewLine keeps this byte-for-byte with the reference CLI (see NodeParityTest).
            Console.WriteLine(JsonSerializer.Serialize(absoluteFilepaths, new JsonSerializerOptions
            {
                WriteIndented = true, NewLine = "\n"
            }));
            return 0;
        }

        Dictionary<string, object> tree = ArrayToTree(filepaths);
        _logger.Debug($"tree: {JsonSerializer.Serialize(tree)}");

        string treeified = Treeify(tree);
        _logger.Info(treeified);

        return 0;
    }
}