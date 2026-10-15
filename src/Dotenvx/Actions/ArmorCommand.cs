using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/armor.js and cli/actions/armor/*.js (hidden in the root help, advertised by
/// the "For Security Teams" section). `armor &lt;other&gt;` runs an external `dotenvx-armor` program (executeDynamic) or
/// prints the Armor banner. Upstream's spinner text ("armoring", "moving", ...) is the TTY-only spinner.
/// </remarks>
public class ArmorCommand : Command
{
    private readonly Argument<string?> _commandArgument = new("command") { Arity = ArgumentArity.ZeroOrOne };

    private readonly Argument<string[]> _argsArgument = new("args") { Arity = ArgumentArity.ZeroOrMore };

    public ArmorCommand(IDotenvxLogger logger) : base("armor", "move private keys into Dotenvx Armor [www.dotenvx.com/armor]")
    {
        Add(_commandArgument);
        Add(_argsArgument);

        Subcommands.Add(new Step("up", "armor key", "team to armor private key for", logger,
            (host, token, device, file, team) => new ArmorUp(host, token, device, file, team).Run(),
            (result, display, file) => $"⛨ armored {file} ({display})"));
        Subcommands.Add(new Step("down", "dearmor key", "team to dearmor private key from", logger,
            (host, token, device, file, team) => new ArmorDown(host, token, device, file, team).Run(),
            (result, display, file) => $"◇ dearmored {file} ({display}) to .env.keys"));
        Subcommands.Add(new Step("push", "push armored key (from .env.keys)", "team to push armored private key for", logger,
            (host, token, device, file, team) => new ArmorPush(host, token, device, file, team).Run(),
            (result, display, file) => $"⛨ pushed {file} ({display}) to Armor"));
        Subcommands.Add(new Step("pull", "pull armored key (into .env.keys)", "team to pull armored private key from", logger,
            (host, token, device, file, team) => new ArmorPull(host, token, device, file, team).Run(),
            (result, display, file) => $"◇ pulled {file} ({display}) to .env.keys"));
        Subcommands.Add(new OpenStep(logger));
        Subcommands.Add(new Step("move", "move armored key (to other team)", null, logger,
            (host, token, device, file, _) => new ArmorMove(host, token, device, file).Run(),
            (result, display, _) => $"⛨ moved to {result.Team} ({display})"));
        Subcommands.Add(new LoginAction(logger, "log in to Dotenvx Armor"));
        Subcommands.Add(new LogoutAction(logger, "log out of Dotenvx Armor"));
        Subcommands.Add(new Command("status", "print armor status")
        {
            Action = new StatusAction()
        });
        Subcommands.Add(new SettingsCommand(logger));

        SetAction(parseResult =>
        {
            string? command = parseResult.GetValue(_commandArgument);
            if (!string.IsNullOrEmpty(command))
            {
                string[] rest = parseResult.GetValue(_argsArgument) ?? [];

                return ExecuteDynamic.Dynamic("armor", [command, .. rest], logger);
            }

            new Session(logger).NotifyUpdate();
            parseResult.RootCommandResult.Command.Parse(["armor", "--help"]).Invoke();

            return 0;
        });
    }

    private sealed class StatusAction : System.CommandLine.Invocation.SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            Console.Out.WriteLine(new Session().Status());

            return 0;
        }
    }

    internal static Option<string?> EnvFile()
    {
        return new("--file", "-f", "--env-file") { Description = "path to your env file", HelpName = "path" };
    }

    internal static Option<string?> Token()
    {
        return new("--token") { Description = "set token", HelpName = "token" };
    }

    private sealed class Step : Command
    {
        private readonly Option<string?> _envFileOption = EnvFile();
        private readonly Option<string?> _teamOption;
        private readonly Option<string?> _tokenOption = Token();

        public Step(string name, string description, string? teamDescription, IDotenvxLogger logger,
            Func<string, string?, string, string?, string?, ArmorResult> run, Func<ArmorResult, string, string, string> success)
            : base(name, description)
        {
            _teamOption = new Option<string?>("--team") { Description = teamDescription ?? "", HelpName = "team" };

            Add(_envFileOption);
            Add(_tokenOption);
            if (teamDescription is not null)
            {
                Add(_teamOption);
            }

            SetAction(parseResult => Execute(parseResult, logger, run, success, teamDescription is not null));
        }

        private static string SpinnerText(string name)
        {
            return name switch
            {
                "up" => "armoring", "down" => "dearmoring", "push" => "pushing", "pull" => "pulling", _ => "moving"
            };
        }

        private int Execute(ParseResult parseResult, IDotenvxLogger logger,
            Func<string, string?, string, string?, string?, ArmorResult> run, Func<ArmorResult, string, string, string> success,
            bool hasTeam)
        {
            using SpinnerScope spinner = SpinnerScope.Start(parseResult, SpinnerText(parseResult.CommandResult.Command.Name));
            string? envFile = parseResult.GetValue(_envFileOption);
            string? tokenOption = parseResult.GetValue(_tokenOption);
            string? team = hasTeam ? parseResult.GetValue(_teamOption) : null;

            var options = new Dictionary<string, string>();
            if (envFile is not null)
            {
                options["envFile"] = envFile;
            }

            if (tokenOption is not null)
            {
                options["token"] = tokenOption;
            }

            if (team is not null)
            {
                options["team"] = team;
            }

            logger.Debug($"options: {JsonStringify(options)}");

            var session = new Session(logger);
            session.NotifyUpdate();
            string hostname = session.Hostname();
            string? token = string.IsNullOrEmpty(tokenOption) ? session.Token() : tokenOption;

            try
            {
                string devicePublicKey = session.DevicePublicKey();
                ArmorResult result = run(hostname, token, devicePublicKey, envFile, team);
                string display = ArmoredKeyDisplay.Display(result.PublicKeyValue);
                if (display == "")
                {
                    display = result.PrivateKeyName;
                }

                if (result.Changed)
                {
                    logger.Success(success(result, display, envFile ?? ".env"));
                }
                else
                {
                    // move.js says "no change ({display})"; the other steps name the env file
                    logger.Info(parseResult.CommandResult.Command.Name == "move"
                        ? $"○ no change ({display})"
                        : $"○ no change to {envFile ?? ".env"} ({display})");
                }

                return 0;
            }
            catch (Exception error)
            {
                logger.Error(error.Message);

                return 1;
            }
        }
    }

    private sealed class OpenStep : Command
    {
        private readonly Option<string?> _envFileOption = EnvFile();

        public OpenStep(IDotenvxLogger logger) : base("open", "open armored key (in browser)")
        {
            Add(_envFileOption);
            SetAction(parseResult =>
            {
                string? envFile = parseResult.GetValue(_envFileOption);
                logger.Debug($"options: {JsonStringify(envFile is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["envFile"] = envFile })}");

                var session = new Session(logger);
                session.NotifyUpdate();
                string hostname = session.Hostname();

                try
                {
                    ArmorOpenResult result = new ArmorOpen(hostname, envFile).Run();
                    string display = ArmoredKeyDisplay.Display(result.PublicKeyValue);

                    OpenUrl.Open(result.Url);
                    logger.Success($"◌ opened ({display})");

                    return 0;
                }
                catch (Exception error)
                {
                    logger.Error(error.Message);

                    return 1;
                }
            });
        }
    }
}
