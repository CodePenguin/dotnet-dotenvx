using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/init.js (hidden `spec`: create an Envfile). Accepted difference: on a terminal
/// upstream asks which discovered env files and whether to scan code (enquirer checklist); this port takes the defaults
/// it pre-selects (every discovered env file, and code scanning).
/// </remarks>
public class SpecAction : Command
{
    private readonly Option<string?> _envFileOption =
        new("--file", "-f", "--env-file") { Description = "file to read variable names from", HelpName = "path" };

    private readonly IDotenvxLogger _logger;
    private readonly Option<bool> _overwriteOption =
        new("--overwrite")
        {
            Description = "replace existing Envfile, including custom rules, comments, and missing-file blocks"
        };

    private readonly Option<bool> _stdoutOption = new("--stdout") { Description = "send to stdout" };

    public SpecAction(IDotenvxLogger logger) : base("spec", "create an Envfile")
    {
        _logger = logger;
        Hidden = true;
        Add(_stdoutOption);
        Add(_overwriteOption);
        Add(_envFileOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string? envFile = parseResult.GetValue(_envFileOption);
        bool overwrite = parseResult.GetValue(_overwriteOption);
        bool stdout = parseResult.GetValue(_stdoutOption);
        bool interactive = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) && SecretCli.IsInteractive();
        SpinnerScope? spinner = null;

        try
        {
            FileSystemInfo? existing = stdout ? null : File.Exists("Envfile") ? new FileInfo("Envfile") :
                Directory.Exists("Envfile") ? new DirectoryInfo("Envfile") : null;
            if (existing is not null && !overwrite)
            {
                _logger.Info("○ Envfile already exists [edit or run: spec --overwrite]");

                return 0;
            }

            if (existing is not null && existing is not FileInfo)
            {
                throw new Exception("Cannot replace Envfile: expected a regular file");
            }

            List<string>? envFiles = null;
            List<string> sourceKeys = [];
            bool scanCode = false;
            if (interactive && envFile is null)
            {
                List<string> candidates = EnvfileInit.DiscoverEnvFiles();
                IReadOnlyList<string> selected = Prompts.Multiselect(new Prompts.MultiselectRequest(
                    existing is not null ? "Recreate Envfile from .env files and code" : "Create Envfile from .env files and code",
                    [.. candidates.Select(candidate => new Prompts.Choice(candidate, candidate)),
                        new Prompts.Choice("code ./**/* (env references)", "__scan_source")])
                {
                    Initial = [.. candidates, "__scan_source"],
                    SubmitLabel = existing is not null ? "Recreate Envfile" : "Create Envfile"
                }) ?? [];
                envFiles = [.. selected.Where(value => value != "__scan_source")];
                scanCode = selected.Contains("__scan_source");
            }

            spinner = SpinnerScope.Start(parseResult, "scanning");
            if (scanCode)
            {
                sourceKeys = EnvfileInit.ScanSource(null, name => spinner.OnStatus($"scanning {name}")).Keys;
            }

            InitEnvfileResult result = EnvfileInit.Init(null, envFile, envFiles, sourceKeys, overwrite, stdout,
                name => spinner.OnStatus($"scanning {name}"));
            spinner.Dispose();
            if (stdout)
            {
                Console.Out.Write(result.Content);

                return 0;
            }

            if (result.Created)
            {
                _logger.Success(result.Replaced ? "◈ recreated (Envfile)" : "◈ created (Envfile)");
                _logger.Help("⮕ next run [dotenvx check]");
            }
            else
            {
                _logger.Info("○ Envfile already exists [edit or run: spec --overwrite]");
            }

            return 0;
        }
        catch (Exception error)
        {
            spinner?.Dispose();
            CatchAndLog(_logger, error);

            return 1;
        }
    }
}
